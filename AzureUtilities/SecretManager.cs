using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography.X509Certificates;
using Azure.Extensions.AspNetCore.Configuration.Secrets;

namespace AzureUtilities
{
    public class SecretManager
    {
        public SecretManager()
        {

        }

        private void GetKeyVaultWithCertAndClientID()
        {

        }

        /// <summary>
        /// Retrieve the specified secret value from the Azure Key Vault at the specified location.
        /// If the webapp is deployed in Azure, access to the secret should be managed using a Managed Identity.
        /// For more information see: https://docs.microsoft.com/en-us/azure/key-vault/general/tutorial-net-create-vault-azure-web-app
        /// and: https://docs.microsoft.com/en-us/aspnet/core/security/key-vault-configuration?view=aspnetcore-6.0
        /// </summary>
        /// <param name="KeyVaultUri"></param>
        /// <param name="SecretName"></param>
        /// <returns></returns>
        public static string GetSecretValueWithManagedIdentity(string KeyVaultUri, string SecretName)
        {
            SecretClientOptions options = new SecretClientOptions()
            {
                Retry =
                {
                    Delay= TimeSpan.FromSeconds(2),
                    MaxDelay = TimeSpan.FromSeconds(16),
                    MaxRetries = 5,
                    Mode = RetryMode.Exponential
                 }
            };
            var client = new SecretClient(new Uri(KeyVaultUri), new DefaultAzureCredential(true), options);

            KeyVaultSecret secret = client.GetSecret(SecretName);

            return secret.Value;
        }

        public static string GetSecretValueWithCertAndClientID(string KeyVaultName, string AzureADDirectoryID, string AzureADApplicationID, string CertThumbprint, string SecretName)
        {
            ConfigurationBuilder builder = new ConfigurationBuilder();

            using var store = new X509Store(StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            X509Certificate2Collection certs = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                CertThumbprint, false);

            builder.AddAzureKeyVault(new Uri($"https://{KeyVaultName}.vault.azure.net/"),
                                    new ClientCertificateCredential(AzureADDirectoryID, AzureADApplicationID, certs.OfType<X509Certificate2>().Single()),
                                    new KeyVaultSecretManager());
            IConfigurationRoot config = builder.Build();

            return config[SecretName];
        }
    }
}