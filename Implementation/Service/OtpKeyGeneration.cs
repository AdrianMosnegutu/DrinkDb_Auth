namespace DrinkDb_Auth.Service
{
    public class OtpKeyGeneration : IKeyGeneration
    {
        public byte[] GenerateRandomKey(int keyLength)
        {
            return OtpNet.KeyGeneration.GenerateRandomKey(keyLength);
        }
    }
}
