using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Azure.Messaging.EventHubs.Producer;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace EventHubsReceiver // Note: actual namespace depends on the project name.
{
    public class Program
    {

        //private const string ehubNamespaceConnectionString = "Endpoint=sb://ryan42-evh-ns.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=B72o/LeeDog2bj3YqCY8i/zEFfPgf/RTWQeHfBw/Bws=;EntityPath=ryan42-eh";
        private const string eventHubName = "ryan42-eh";
        //private const string blobStorageConnectionString = "DefaultEndpointsProtocol=https;AccountName=videoframestorage;AccountKey=KHsOqZQ2pzQpZaOPDMrBJo1pmseJcgod/CZNu3iKMWJG32VFovisrpQ6WOD2wIFie0kpb/UCwyXqAUfOHjaC8w==;EndpointSuffix=core.windows.net";
        private const string blobContainerName = "video-frames";
        private const string KV_EH_CONN_STR_NAME = "ryan42-eventhub-conn-str";
        private const string KV_BLOB_CONN_STR_NAME = "videoframe-blob-connection-string";

        static BlobContainerClient storageClient;

        // The Event Hubs client types are safe to cache and use as a singleton for the lifetime
        // of the application, which is best practice when events are being published or read regularly.
        static EventProcessorClient processor;

        // The Event Hubs client types are safe to cache and use as a singleton for the lifetime
        // of the application, which is best practice when events are being published or read regularly.
        static EventHubProducerClient producerClient;

        private static void GetAzureAppConfigurationValues(out string KeyVaultName, out string AzureADDirectoryID, out string AzureADApplicationID, out string Thumbprint)
        {
            // Parse appSetting.json
            ConfigurationBuilder builder = new ConfigurationBuilder();
            builder.SetBasePath(Directory.GetCurrentDirectory());
            builder.AddJsonFile("appSettings.json", optional: false);
            IConfigurationRoot builtConfig = builder.Build();

            // Extract values from appSettings
            KeyVaultName = builtConfig["KeyVaultName"];
            AzureADDirectoryID = builtConfig["AzureADDirectoryId"];
            AzureADApplicationID = builtConfig["AzureADApplicationId"];
            Thumbprint = builtConfig["AzureADCertThumbprint"];
        }

        public static async Task Main(string[] args)
        {
            // Get all of the configuration information needed to connect to the key vault
            string keyVaultName, azADDirectoryID, azADApplicationID, azThumbprint;
            GetAzureAppConfigurationValues(out keyVaultName, out azADDirectoryID, out azADApplicationID, out azThumbprint);
            
            string blobStorageConnectionString = string.Empty;// TODO: Retrieve blob connection string from AKV
            string ehubNamespaceConnectionString = AzureUtilities.SecretManager.GetSecretValueWithCertAndClientID(keyVaultName, azADDirectoryID, azADApplicationID, azThumbprint, "ryan42-eventhub-conn-str");

            // Read from the default consumer group
            string consumerGroup = EventHubConsumerClient.DefaultConsumerGroupName;

            // Create a blob container client that the event processor will use
            storageClient = new BlobContainerClient(blobStorageConnectionString, blobContainerName);

            // Create an event processor client to process events in the event hub
            processor = new EventProcessorClient(storageClient, consumerGroup, ehubNamespaceConnectionString);

            // Register handlers for processing events and handling errors
            processor.ProcessEventAsync += ProcessEventHandler;
            processor.ProcessErrorAsync += ProcessErrorHandler;

            // Start the processing
            await processor.StartProcessingAsync();

            // Wait for 120 seconds for the events to be processed
            await Task.Delay(TimeSpan.FromSeconds(120));

            // Stop the processing
            await processor.StopProcessingAsync();
        }

        static async Task ProcessEventHandler(ProcessEventArgs eventArgs)
        {
            // Write the body of the event to the console window
            Console.WriteLine("\tReceived event: {0}", Encoding.UTF8.GetString(eventArgs.Data.Body.ToArray()));
            Console.WriteLine("\tCorrelation ID: {0}", eventArgs.Data.CorrelationId);

            // Update checkpoint in the blob storage so that the app receives only new events the next time it's run
            await eventArgs.UpdateCheckpointAsync(eventArgs.CancellationToken);
        }

        static Task ProcessErrorHandler(ProcessErrorEventArgs eventArgs)
        {
            // Write details about the error to the console window
            Console.WriteLine($"\tPartition '{ eventArgs.PartitionId}': an unhandled exception was encountered. This was not expected to happen.");
            Console.WriteLine(eventArgs.Exception.Message);
            return Task.CompletedTask;
        }
    }
}
