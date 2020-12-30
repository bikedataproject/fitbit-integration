using System;
using Fitbit.Api.Portable;
using Fitbit.Models;
using Xunit;

namespace BikeDataProject.Integrations.Fitbit.Tests.Fitbit
{
    public class SubscriptionManagerTests
    {
        [Fact]
        public void SubscriptionManager_ProcessUpdateResponseBody_0UpdateResources_ShouldParseEmptyList()
        {
            var json =
                "[\r\n   ]";

            var response = SubscriptionManager.ProcessUpdateReponseBody(json);
            
            Assert.NotNull(response);
            Assert.Empty(response);
        }
        
        [Fact]
        public void SubscriptionManager_ProcessUpdateResponseBody_1UpdateResource_ShouldParse1()
        {
            var json =
                "[\r\n    {\r\n        \"collectionType\": \"activities\",\r\n        \"date\": \"2020-12-30\",\r\n        \"ownerId\": \"8VMRJS\",\r\n        \"ownerType\": \"user\",\r\n        \"subscriptionId\": \"291b806c-883d-406f-a51c-0d7859b4ca3f-activities\"\r\n    }\r\n]";

            var response = SubscriptionManager.ProcessUpdateReponseBody(json);
            
            Assert.NotNull(response);
            Assert.Single(response);
            var ur = response[0];
            Assert.NotNull(ur);
            Assert.Equal("8VMRJS", ur.OwnerId);
            Assert.Equal(ResourceOwnerType.User, ur.OwnerType);
            Assert.Equal("291b806c-883d-406f-a51c-0d7859b4ca3f-activities", ur.SubscriptionId);
            Assert.Equal(APICollectionType.activities, ur.CollectionType);
            Assert.Equal(new DateTime(2020,12, 30, 0,0,0, DateTimeKind.Unspecified), ur.Date);
        }
    }
}
