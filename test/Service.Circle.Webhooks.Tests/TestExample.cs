using System;
using Newtonsoft.Json;
using NUnit.Framework;
using Service.Circle.Webhooks.Services;

namespace Service.Circle.Webhooks.Tests
{
    public class TestExample
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            string body = @"{ ""Type"" : ""Notification"",
              ""MessageId"" : ""0d352340-bf-86dec277baf"",
              ""TopicArn"" : ""arn:aws:sns:us-east-1:908968368384:prod_platform-notifications-topic"",
              ""Message"" : ""{\""clientId\"":\""320b9ecc851ba9\"",\""notificationType\"":\""payments\"",\""version\"":1,\""customAttributes\"":{\""clientId\"":\""320b9ecf-3b33-45e5-ac77-2e4adc851ba9\""},\""payment\"":{\""id\"":\""39c75eb5-444c-4fcc-87bb-272d64d1bf82\"",\""type\"":\""payment\"",\""status\"":\""failed\"",\""description\"":\""jetwallet|-|8f574fe7c7a04d2b8eed96a3dec60dbb|-|SP-8f574fe7c7a04d2b8eed96a3dec60dbb\"",\""amount\"":{\""amount\"":\""50.00\"",\""currency\"":\""USD\""},\""createDate\"":\""2022-06-22T17:28:39.815Z\"",\""updateDate\"":\""2022-06-22T17:28:39.815Z\"",\""merchantId\"":\""320b9ecf-3b33-45e5-ac77-2e4dacc851ba9\"",\""merchantWalletId\"":\""2003226\"",\""source\"":{\""id\"":\""0da641da-d4db-43a0-ae3b-61094cc5e70f\"",\""type\"":\""card\""},\""errorCode\"":\""payment_denied\"",\""refunds\"":[],\""metadata\"":{\""phoneNumber\"":\""+1234567890\"",\""email\"":\""test@mailinator.com\""}}}"",
              ""Timestamp"" : ""2022-06-22T17:28:40.368Z"",
              ""SignatureVersion"" : ""1"",
              ""Signature"" : ""IYJ878W6X+zMlp3KVUx9eLeINEIkZ7OdVgKuO0ThLwBjfUH1anwoySYFQZdW/adZpOHrPoOvYEl57kMq25I3ssMGtvzsJy1Y8cGCY9vnoATYP5+Bf+XwNRZZPJGA6XBgmAOD2vF3V+sXOP7txSmt6hEowDBJm9Dn3aCt0VZoFyRSKI7DE8CcZAwcJwhnCVMQ2fa3p1l6jLNzjCaqxakfPJNTFvLVEukogH0KL5/QQmQ9fw7q3rzuwP4pDjkVbV/uLPOpK51ksgpv3OcjqgVvXHXj+Mi8sS2BUWV9iSImiWHGn3L9vYg5mibs2tgI/2CPplEOaMjss7MeG5nV7/dHVZlg=="",
              ""SigningCertURL"" : ""https://test.com/SimpleNotificationService-7ff5318490ec183fbaddaa2a969abfda.pem"",
              ""UnsubscribeURL"" : ""https://test.com/?Action=Unsubscribe&SubscriptionArn=arn:aws:sns:us-east-1:908968368384:test_platform-notifications-topic:61ccb838ad1"",
              ""MessageAttributes"" : {
                ""clientId"" : {""Type"":""String"",""Value"":""320b9ecf-3b33-45e5-ac77-2e4adc851ba9""}
              }
            }";
                        
            try
            {
                var dto = JsonConvert.DeserializeObject<WebhookMiddleware.NotificationDto>(body);
                var message = JsonConvert.DeserializeObject<WebhookMiddleware.MessageDto>(dto.Message);
                var (brokerId, clientId, walletId) = ParseDescription(message.Payment.Description);


            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Assert.Fail();
            }

            
            Console.WriteLine("Debug output");
            Assert.Pass();
        }
        
        public (string, string, string) ParseDescription(string description)
        {
            if (string.IsNullOrEmpty(description))
                return (null, null, null);

            var prm = description.Split("|-|");

            if (prm.Length != 3)
                return (null, null, null);

            if (string.IsNullOrEmpty(prm[0]) || string.IsNullOrEmpty(prm[1]) || string.IsNullOrEmpty(prm[2]))
                return (null, null, null);

            return (prm[0], prm[1], prm[2]);
        }
    }
}
