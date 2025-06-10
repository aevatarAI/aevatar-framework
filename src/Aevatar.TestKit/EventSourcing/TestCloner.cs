using Orleans.Serialization.Cloning;

public class TestCloner<T> : IDeepCopier<T>
{
    public T DeepCopy(T input, CopyContext context)
    {
        // Implement deep copy logic here
        return input;
    }
}