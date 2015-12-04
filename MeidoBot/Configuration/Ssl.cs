// For dealing with SSL/TLS.
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;


namespace MeidoBot
{
    static class Ssl
    {
        public static void EnableTrustAll()
        {
            // State we want to use our ACCEPT ALL implementation.
            ServicePointManager.ServerCertificateValidationCallback = TrustAllCertificates;
        }

        // Implement an ACCEPT ALL Certificate Policy (for SSL).
        // What we're going to do is not security sensitive. SSL or not, we don't care,
        public static bool TrustAllCertificates(object sender, X509Certificate cert, X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}