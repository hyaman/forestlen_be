namespace ForestIQ.Domain.Interface.Licensing
{
    public interface IRsaHelper
    {
        string SignData(string data);
        bool VerifyData(string data, string signature);
    }
}
