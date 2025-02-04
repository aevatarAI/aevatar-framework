using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Aevatar.AI.Common;
using Aevatar.AI.Model;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using Aevatar.AI.Brain;

namespace Aevatar.AI.EmbeddedDataLoader.EmbeddedPdf;

internal class EmbeddedPftDataLoader<TKey>(
    UniqueKeyGenerator<TKey> uniqueKeyGenerator,
    IVectorStoreRecordCollection<TKey, TextSnippet<TKey>> vectorStoreCollection,
    ITextEmbeddingGenerationService textEmbeddingGenerationService,
    IChatCompletionService chatCompletionService) : IEmbeddedDataLoader where TKey : notnull
{
    public async Task Load(FileData fileData, int batchSize, int betweenBatchDelayInMs, CancellationToken cancellationToken)
    {
        // Create the collection if it doesn't exist.
        await vectorStoreCollection.CreateCollectionIfNotExistsAsync(cancellationToken).ConfigureAwait(false);

        // Load the text and images from the PDF file and split them into batches.
        if (fileData.Content != null)
        {
            var sections = LoadTextAndImages(fileData.Content, cancellationToken);
            var batches = sections.Chunk(batchSize);

            // Process each batch of content items.
            foreach (var batch in batches)
            {
                // Convert any images to text.
                var textContentTasks = batch.Select(async content =>
                {
                    if (content.Text != null)
                    {
                        return content;
                    }

                    var textFromImage = await ConvertImageToTextWithRetryAsync(
                        chatCompletionService,
                        content.Image!.Value,
                        cancellationToken).ConfigureAwait(false);
                    return new RawContent { Text = textFromImage, PageNumber = content.PageNumber };
                });
                var textContent = await Task.WhenAll(textContentTasks).ConfigureAwait(false);

                // Map each paragraph to a TextSnippet and generate an embedding for it.
                var recordTasks = textContent.Select(async content => new TextSnippet<TKey>
                {
                    Key = uniqueKeyGenerator.GenerateKey(),
                    Text = content.Text,
                    ReferenceDescription = $"{fileData.Name}#page={content.PageNumber}",
                    //ReferenceLink = $"{new Uri(file.Name).AbsoluteUri}#page={content.PageNumber}",
                    ReferenceLink = $"{fileData.Name}#page={content.PageNumber}",
                    TextEmbedding = await GenerateEmbeddingsWithRetryAsync(textEmbeddingGenerationService, content.Text!,
                        cancellationToken: cancellationToken).ConfigureAwait(false)
                });

                // Upsert the records into the vector store.
                var records = await Task.WhenAll(recordTasks).ConfigureAwait(false);
                var upsertedKeys =
                    vectorStoreCollection.UpsertBatchAsync(records, cancellationToken: cancellationToken);
                await foreach (var key in upsertedKeys.ConfigureAwait(false))
                {
                    Console.WriteLine($"Upserted record '{key}' into VectorDB");
                }

                await Task.Delay(betweenBatchDelayInMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }
        
    private static IEnumerable<RawContent> LoadTextAndImages(byte[] fileBytes, CancellationToken cancellationToken)
    {
        using (var document = PdfDocument.Open(fileBytes))
        {
            foreach (var page in document.GetPages())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                foreach (var image in page.GetImages())
                {
                    if (image.TryGetPng(out var png))
                    {
                        yield return new RawContent { Image = png, PageNumber = page.Number };
                    }
                    else
                    {
                        Console.WriteLine($"Unsupported image format on page {page.Number}");
                    }
                }

                var blocks = DefaultPageSegmenter.Instance.GetBlocks(page.GetWords());
                foreach (var block in blocks)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    yield return new RawContent { Text = block.Text, PageNumber = page.Number };
                }
            }
        }
    }

    /// <summary>
    /// Add a simple retry mechanism to embedding generation.
    /// </summary>
    /// <param name="textEmbeddingGenerationService">The embedding generation service.</param>
    /// <param name="text">The text to generate the embedding for.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The generated embedding.</returns>
    private static async Task<ReadOnlyMemory<float>> GenerateEmbeddingsWithRetryAsync(ITextEmbeddingGenerationService textEmbeddingGenerationService, string text, CancellationToken cancellationToken)
    {
        var tries = 0;

        while (true)
        {
            try
            {
                return await textEmbeddingGenerationService.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (HttpOperationException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                tries++;

                if (tries < 3)
                {
                    Console.WriteLine($"Failed to generate embedding. Error: {ex}");
                    Console.WriteLine("Retrying embedding generation...");
                    await Task.Delay(10_000, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Add a simple retry mechanism to image to text.
    /// </summary>
    /// <param name="chatCompletionService">The chat completion service to use for generating text from images.</param>
    /// <param name="imageBytes">The image to generate the text for.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The generated text.</returns>
    private static async Task<string> ConvertImageToTextWithRetryAsync(
        IChatCompletionService chatCompletionService,
        ReadOnlyMemory<byte> imageBytes,
        CancellationToken cancellationToken)
    {
        var tries = 0;

        while (true)
        {
            try
            {
                var chatHistory = new ChatHistory();
                chatHistory.AddUserMessage([
                    new TextContent("What’s in this image?"),
                    new ImageContent(imageBytes, "image/png"),
                ]);
                var result = await chatCompletionService.GetChatMessageContentsAsync(chatHistory, cancellationToken: cancellationToken).ConfigureAwait(false);
                return string.Join("\n", result.Select(x => x.Content));
            }
            catch (HttpOperationException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                tries++;

                if (tries < 3)
                {
                    Console.WriteLine($"Failed to generate text from image. Error: {ex}");
                    Console.WriteLine("Retrying text to image conversion...");
                    await Task.Delay(10_000, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Private model for returning the content items from a PDF file.
    /// </summary>
    private sealed class RawContent
    {
        public string? Text { get; init; }

        public ReadOnlyMemory<byte>? Image { get; init; }

        public int PageNumber { get; init; }
    }
}