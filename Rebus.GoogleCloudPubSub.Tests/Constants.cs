using System;
using System.IO;
using Newtonsoft.Json;

namespace Rebus.GoogleCloudPubSub.Tests
{
    public static class Constants
    {
        public static string GoogleCredentialsPath => Path.Combine(AppContext.BaseDirectory, "google-cloud-credentials.json");
        public static string ProjectId => JsonConvert.DeserializeObject<GoogleCredentials>(File.ReadAllText(GoogleCredentialsPath)).ProjectId;
        public const string Receiver = "receiver";
        public const string Sender = "sender";

    }
}