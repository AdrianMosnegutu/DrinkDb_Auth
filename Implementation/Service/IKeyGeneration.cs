namespace DrinkDb_Auth.Service
{
    public interface IKeyGeneration
    {
        byte[] GenerateRandomKey(int keyLength);
    }
}
