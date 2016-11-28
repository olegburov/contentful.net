﻿using Contentful.Core.Configuration;
using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Contentful.Core.Tests
{
    public class ContentfulManagementClientTests
    {
        private ContentfulManagementClient _client;
        private FakeMessageHandler _handler;
        private HttpClient _httpClient;
        public ContentfulManagementClientTests()
        {
            _handler = new FakeMessageHandler();
            _httpClient = new HttpClient(_handler);
            _client = new ContentfulManagementClient(_httpClient, new ContentfulOptions()
            {
                DeliveryApiKey = "123",
                ManagementApiKey = "564",
                SpaceId = "666",
                UsePreviewApi = false
            });
        }

        [Fact]
        public void CreatingManagementClientShouldSetHeadersCorrectly()
        {
            //Arrange
            
            //Act
            //Assert
            Assert.Equal("564", _httpClient.DefaultRequestHeaders.Authorization.Parameter);
        }
        
        [Fact]
        public async Task CreateContentTypeShouldSerializeRequestCorrectly()
        {
            //Arrange
            _handler.Response = new HttpResponseMessage() {
                Content = new StringContent("{}")
            };
            var contentType = new ContentType();
            contentType.SystemProperties = new SystemProperties();
            contentType.SystemProperties.Id = "123";
            contentType.Name = "Name";
            contentType.Fields = new List<Field>()
            {
                new Field()
                {
                    Name = "Field1",
                    Id = "field1",
                    @Type = "Symbol",
                    Required = true,
                    Localized = false,
                    Disabled = false,
                    Omitted = false
                },
                new Field()
                {
                    Name = "Field2",
                    Id = "field2",
                    @Type = "Location",
                    Required = false,
                    Localized = true,
                    Disabled = false,
                    Omitted = false
                },
                new Field()
                {
                    Name = "Field3",
                    Id = "field3",
                    @Type = "Text",
                    Required = false,
                    Localized = false,
                    Disabled = true,
                    Omitted = false,
                    Validations = new List<IFieldValidator>()
                    {
                        new SizeValidator(3,100)
                    }
                },
                new Field()
                {
                    Name = "Field4",
                    Id = "field4",
                    @Type = "Link",
                    Required = false,
                    Localized = false,
                    Disabled = false,
                    Omitted = false,
                    LinkType = "Asset"
                }
            };


            //Act
            var res = await _client.CreateOrUpdateContentTypeAsync(contentType);

            //Assert
            Assert.Null(res.Name);
        }

        [Fact]
        public async Task EditorInterfaceShouldDeserializeCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\EditorInterface.json");

            //Act
            var res = await _client.GetEditorInterfaceAsync("someid");

            //Assert
            Assert.Equal(7, res.Controls.Count);
            Assert.IsType<BooleanEditorInterfaceControlSettings>(res.Controls[4].Settings);
        }

        [Fact]
        public async Task CreateSpaceShouldCreateCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleSpace.json");
            var headerSet = false;
            var contentSet = "";
            _handler.VerificationBeforeSend = () =>
            {
                 headerSet = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Organization").First() == "112";
            };
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
            };

            //Act
            var res = await _client.CreateSpaceAsync("Spaceman", "en-US", "112");

            //Assert
            Assert.True(headerSet);
            Assert.Equal(@"{""name"":""Spaceman"",""defaultLocale"":""en-US""}", contentSet);
            Assert.Equal("Products", res.Name);
        }

        [Fact]
        public async Task UpdateSpaceShouldCreateCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleSpace.json");
            var headerSet = false;
            var contentSet = "";
            _handler.VerificationBeforeSend = () =>
            {
                headerSet = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First() == "37";
            };
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
            };

            //Act
            var res = await _client.UpdateSpaceNameAsync("spaceId", "Spacemaster", 37, "333");

            //Assert
            Assert.True(headerSet);
            Assert.Equal(@"{""name"":""Spacemaster""}", contentSet);
            Assert.Equal("Products", res.Name);
        }

        [Theory]
        [InlineData("123")]
        [InlineData("54")]
        [InlineData("345")]
        public async Task DeletingASpaceShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = new HttpResponseMessage();
            var requestUrl = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };

            //Act
            await _client.DeleteSpaceAsync(id);

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal($"https://api.contentful.com/spaces/{id}", requestUrl);
        }

        [Fact]
        public async Task GetContentTypesShouldDeserializeCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\ContenttypesCollectionManagement.json");
            //Act
            var res = await _client.GetContentTypesAsync();

            //Assert
            Assert.Equal(4, res.Count());
            Assert.Equal("someName", res.First().Name);
            Assert.Equal(8, (res.First().Fields.First().Validations.First() as SizeValidator).Max);
        }

        [Fact]
        public async Task CreateOrUpdateContentTypeShouldThrowIfNoIdSet()
        {
            //Arrange
            var contentType = new ContentType();
            contentType.Name = "Barbossa";
            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.CreateOrUpdateContentTypeAsync(contentType));

            //Assert
            Assert.Equal("The id of the content type must be set.\r\nParameter name: contentType", ex.Message);
        }

        [Fact]
        public async Task CreateOrUpdateContentTypeShouldCreateCorrectObject()
        {
            //Arrange
            var contentType = new ContentType();
            contentType.Name = "Barbossa";
            contentType.SystemProperties = new SystemProperties();
            contentType.SystemProperties.Id = "323";
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleContentType.json");

            var versionHeader = "";
            var contentSet = "";
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
            };

            //Act
            var res = await _client.CreateOrUpdateContentTypeAsync(contentType, version: 22);

            //Assert
            Assert.Equal("22", versionHeader);
            Assert.Contains(@"""name"":""Barbossa""", contentSet);
        }

        [Fact]
        public async Task GetContentTypeShouldSerializeCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleContentType.json");

            //Act
            var res = await _client.GetContentTypeAsync("someid");

            //Assert
            Assert.Equal("Product", res.Name);
            Assert.Equal("productName", res.DisplayField);
            Assert.Equal(12, res.Fields.Count);
            Assert.True(res.Fields[0].Localized);
            Assert.Equal("Description", res.Fields[2].Name);
            Assert.Equal("Link", res.Fields[4].Items.Type);

        }

        [Fact]
        public async Task GetContentTypeShouldThrowForEmptyId()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleContentType.json");

            //Act
            var res = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.GetContentTypeAsync(""));

            //Assert
            Assert.Equal("contentTypeId", res.Message);
        }

        [Theory]
        [InlineData("777")]
        [InlineData("456")]
        [InlineData("666")]
        public async Task DeletingAContentTypeShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = new HttpResponseMessage();
            var requestUrl = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };

            //Act
            await _client.DeleteContentTypeAsync(id);

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal($"https://api.contentful.com/spaces/666/content_types/{id}", requestUrl);
        }

        [Fact]
        public async Task ActivatingContentTypeShouldThrowForEmptyId()
        {
            //Arrange
            
            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.ActivateContentTypeAsync("", 34));

            //Assert
            Assert.Equal("contentTypeId", ex.Message);
        }

        [Fact]
        public async Task ActivatingContentTypeShouldCallCorrectUrl()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleContentType.json");

            var versionHeader = "";
            var requestUrl = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };

            //Act
            var res = await _client.ActivateContentTypeAsync("758", 345);

            //Assert
            Assert.Equal(HttpMethod.Put, requestMethod);
            Assert.Equal($"https://api.contentful.com/spaces/666/content_types/758/published", requestUrl);
            Assert.Equal("345", versionHeader);
        }

        [Fact]
        public async Task DeactivatingContentTypeShouldThrowForEmptyId()
        {
            //Arrange

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.DeactivateContentTypeAsync(""));

            //Assert
            Assert.Equal("contentTypeId", ex.Message);
        }

        [Fact]
        public async Task DeactivatingContentTypeShouldCallCorrectUrl()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleContentType.json");

            var requestUrl = "";
            var requestMethod = HttpMethod.Trace;

            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };

            //Act
            await _client.DeactivateContentTypeAsync("324");

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal($"https://api.contentful.com/spaces/666/content_types/324/published", requestUrl);
        }

        [Fact]
        public async Task GetActivatedContentTypesShouldSerializeCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\ContenttypesCollectionManagement.json");
            var requestUrl = "";

            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestUrl = request.RequestUri.ToString();
            };

            //Act
            var res = await _client.GetActivatedContentTypesAsync();

            //Assert
            Assert.Equal(4, res.Count());
            Assert.Equal($"https://api.contentful.com/spaces/666/public/content_types", requestUrl);
            Assert.Equal("someName", res.First().Name);
            Assert.Equal(8, (res.First().Fields.First().Validations.First() as SizeValidator).Max);

        }

        [Fact]
        public async Task GetEditorInterfaceShouldThrowForEmptyId()
        {
            //Arrange

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.GetEditorInterfaceAsync(""));
            //Assert
            Assert.Equal("contentTypeId", ex.Message);
        }

        [Fact]
        public async Task GetEditorInterfaceShouldReturnCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\EditorInterface.json");

            //Act
            var res = await _client.GetEditorInterfaceAsync("23");
            //Assert
            Assert.Equal(7, res.Controls.Count);
            Assert.IsType<BooleanEditorInterfaceControlSettings>(res.Controls[4].Settings);
            Assert.IsType<RatingEditorInterfaceControlSettings>(res.Controls[5].Settings);
            Assert.Equal(7, (res.Controls[5].Settings as RatingEditorInterfaceControlSettings).NumberOfStars);
            Assert.Equal("How many do you likez?", res.Controls[5].Settings.HelpText);
            Assert.IsType<DatePickerEditorInterfaceControlSettings>(res.Controls[6].Settings);
            Assert.Equal(EditorInterfaceDateFormat.time, (res.Controls[6].Settings as DatePickerEditorInterfaceControlSettings).DateFormat);

        }

        [Fact]
        public async Task UpdateEditorInterfaceShouldThrowForEmptyId()
        {
            //Arrange
            var editorInterface = new EditorInterface();
            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.UpdateEditorInterfaceAsync(editorInterface, "", 6));
            //Assert
            Assert.Equal("contentTypeId", ex.Message);
        }

        [Fact]
        public async Task UpdateEditorInterfaceShouldCallCorrectUrl()
        {
            //Arrange
            var editorInterface = new EditorInterface();
            editorInterface.Controls = new List<EditorInterfaceControl>()
            {
                new EditorInterfaceControl()
                {
                    FieldId = "field1",
                    WidgetId = SystemWidgetIds.SingleLine
                },
                new EditorInterfaceControl()
                {
                    FieldId = "field2",
                    WidgetId = SystemWidgetIds.Boolean,
                    Settings = new BooleanEditorInterfaceControlSettings()
                    {
                        HelpText = "Help me here!",
                        TrueLabel = "Truthy",
                        FalseLabel = "Falsy"
                    }
                }
            };
            var versionHeader = "";
            var contentSet = "";
            var requestUrl = "";
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                requestUrl = request.RequestUri.ToString();
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
            };
            _handler.Response = GetResponseFromFile(@"JsonFiles\EditorInterface.json");

            //Act
            var res = await _client.UpdateEditorInterfaceAsync(editorInterface, "123", 16);

            //Assert
            Assert.Equal("16", versionHeader);
            Assert.Equal("https://api.contentful.com/spaces/666/content_types/123/editor_interface", requestUrl);
            Assert.Equal(@"{""controls"":[{""fieldId"":""field1"",""widgetId"":""singleLine""},{""fieldId"":""field2"",""widgetId"":""boolean"",""settings"":{""trueLabel"":""Truthy"",""falseLabel"":""Falsy"",""helpText"":""Help me here!""}}]}", contentSet);
        }

        [Fact]
        public async Task EditorInterfaceShouldSerializeCorrectly()
        {
            //Arrange
            var editorInterface = new EditorInterface();
            editorInterface.Controls = new List<EditorInterfaceControl>()
            {
                new EditorInterfaceControl()
                {
                    FieldId = "field1",
                    WidgetId = SystemWidgetIds.SingleLine
                },
                new EditorInterfaceControl()
                {
                    FieldId = "field2",
                    WidgetId = SystemWidgetIds.Boolean,
                    Settings = new BooleanEditorInterfaceControlSettings()
                    {
                        HelpText = "Help me here!",
                        TrueLabel = "Truthy",
                        FalseLabel = "Falsy"
                    }
                }
            };
            _handler.Response = GetResponseFromFile(@"JsonFiles\EditorInterface.json");

            //Act
            var res = await _client.UpdateEditorInterfaceAsync(editorInterface, "123", 1);

            //Assert
            Assert.Equal(7, res.Controls.Count);
            Assert.IsType<BooleanEditorInterfaceControlSettings>(res.Controls[4].Settings);
        }

        [Fact]
        public async Task GetEntriesCollectionShouldSerializeIntoCorrectCollection()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\EntriesCollectionManagement.json");

            //Act
            var res = await _client.GetEntriesCollectionAsync<Entry<dynamic>>();

            //Assert
            Assert.Equal(8, res.Total);
            Assert.Equal(100, res.Limit);
            Assert.Equal(0, res.Skip);
            Assert.Equal(8, res.Items.Count());
            Assert.Equal("Somethi", res.First().Fields.field1["en-US"].ToString());
            Assert.Equal(DateTime.Parse("2016-11-23T09:40:56.857Z").ToUniversalTime(), res.First().SystemProperties.CreatedAt);
            Assert.Equal("testagain", res.First().SystemProperties.ContentType.SystemProperties.Id);
        }

        [Fact]
        public async Task CreateOrUpdateEntryShouldThrowIfIdIsNotSet()
        {
            //Arrange
            var entry = new Entry<dynamic>();
            entry.SystemProperties = new SystemProperties();
            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.CreateOrUpdateEntryAsync(entry, contentTypeId: "Hwoarang"));
            //Assert
            Assert.Equal("The id of the entry must be set.", ex.Message);
        }

        [Fact]
        public async Task CreateOrUpdateEntryShouldAddCorrectContentTypeHeader()
        {
            //Arrange
            var entry = new Entry<dynamic>();
            entry.SystemProperties = new SystemProperties();
            entry.SystemProperties.Id = "123";
            var contentTypeHeader = "";
            _handler.VerificationBeforeSend = () =>
            {
                contentTypeHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Content-Type").First();
            };
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleEntryManagement.json");

            //Act
            var res = await _client.CreateOrUpdateEntryAsync(entry, contentTypeId: "Eddie Gordo");
            //Assert
            Assert.Equal("Eddie Gordo", contentTypeHeader);
        }

        [Fact]
        public async Task CreateOrUpdateEntryShouldNotAddContentTypeHeaderIfNotSet()
        {
            //Arrange
            var entry = new Entry<dynamic>();
            entry.SystemProperties = new SystemProperties();
            entry.SystemProperties.Id = "123";
            IEnumerable<string> contentTypeHeader = new List<string>();
            _handler.VerificationBeforeSend = () =>
            {
                _httpClient.DefaultRequestHeaders.TryGetValues("X-Contentful-Content-Type", out contentTypeHeader);
            };
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleEntryManagement.json");
            //Act
            var res = await _client.CreateOrUpdateEntryAsync(entry, version: 7);
            //Assert
            Assert.Null(contentTypeHeader);
        }

        [Fact]
        public async Task CreateOrUpdateEntryShouldCallCorrectUrlWithData()
        {
            //Arrange
            var entry = new Entry<dynamic>();
            entry.SystemProperties = new SystemProperties();
            entry.SystemProperties.Id = "532";
            entry.Fields = new ExpandoObject();
            entry.Fields.field34 = new Dictionary<string, string>()
            {
                { "en-US", "banana" }
            };
            var contentTypeHeader = "";
            var contentSet = "";
            var requestUrl = "";
            var versionHeader = "";
            _handler.VerificationBeforeSend = () =>
            {
                contentTypeHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Content-Type").First();
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                requestUrl = request.RequestUri.ToString();
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
            };
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleEntryManagement.json");

            //Act
            var res = await _client.CreateOrUpdateEntryAsync(entry, contentTypeId: "Bryan Fury", version: 45);
            //Assert
            Assert.Equal("Bryan Fury", contentTypeHeader);
            Assert.Equal("45", versionHeader);
            Assert.Equal("https://api.contentful.com/spaces/666/entries/532", requestUrl);
            Assert.Contains(@"""field34"":{""en-US"":""banana""}", contentSet);
        }

        [Fact]
        public async Task GetEntryShouldThrowIfIdIsNotSet()
        {
            //Arrange

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.GetEntryAsync(""));
            //Assert
            Assert.Equal("entryId", ex.Message);
        }

        [Fact]
        public async Task GetEntryShouldReturnCorrectObject()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleEntryManagement.json");

            //Act
            var res = await _client.GetEntryAsync("43");
            //Assert
            Assert.Equal("42BwcCt4TeeskG0eq8S2CQ", res.SystemProperties.Id);
            Assert.Equal("Somethi", res.Fields.field1["en-US"].ToString());
        }

        [Theory]
        [InlineData("777")]
        [InlineData("456")]
        [InlineData("666")]
        public async Task DeletingAnEntryShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = new HttpResponseMessage();
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };

            //Act
            await _client.DeleteEntryAsync(id, 32);

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal("32", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/entries/{id}", requestUrl);
        }

        [Theory]
        [InlineData("777")]
        [InlineData("abc")]
        [InlineData("666")]
        public async Task PublishingAnEntryShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = new HttpResponseMessage();
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleEntryManagement.json");

            //Act
            await _client.PublishEntryAsync(id, 23);

            //Assert
            Assert.Equal(HttpMethod.Put, requestMethod);
            Assert.Equal("23", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/entries/{id}/published", requestUrl);
        }

        [Theory]
        [InlineData("777")]
        [InlineData("abc")]
        [InlineData("666")]
        public async Task UnpublishingAnEntryShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = new HttpResponseMessage();
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleEntryManagement.json");

            //Act
            await _client.PublishEntryAsync(id, 23);

            //Assert
            Assert.Equal(HttpMethod.Put, requestMethod);
            Assert.Equal("23", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/entries/{id}/published", requestUrl);
        }

        [Theory]
        [InlineData("777")]
        [InlineData("abc")]
        [InlineData("666")]
        public async Task ArchivingAnEntryShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = new HttpResponseMessage();
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleEntryManagement.json");

            //Act
            await _client.ArchiveEntryAsync(id, 78);

            //Assert
            Assert.Equal(HttpMethod.Put, requestMethod);
            Assert.Equal("78", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/entries/{id}/archived", requestUrl);
        }

        [Theory]
        [InlineData("777")]
        [InlineData("abc")]
        [InlineData("666")]
        public async Task UnarchivingAnEntryShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = new HttpResponseMessage();
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleEntryManagement.json");

            //Act
            await _client.UnarchiveEntryAsync(id, 67);

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal("67", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/entries/{id}/archived", requestUrl);
        }

        [Fact]
        public async Task GetAllAssetsShouldReturnCorrectCollection()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\AssetsCollectionManagement.json");

            //Act
            var res = await _client.GetAssetsCollectionAsync();

            //Assert
            Assert.Equal(7, res.Count());
            Assert.Equal(7, res.Total);
            Assert.Equal("Ernest Hemingway (1950)", res.First().Title["en-US"]);
            Assert.Equal(2290561, res.First().Files["en-US"].Details.Size);
            Assert.Equal(2940, res.First().Files["en-US"].Details.Image.Width);
            Assert.Equal("Alice in Wonderland", res.Last().Title["en-US"]);
        }

        [Fact]
        public async Task CreateAssetShouldThrowIfIdNotSet()
        {
            //Arrange
            var asset = new ManagementAsset();
            asset.Title = new Dictionary<string, string>();
            asset.Title["en-US"] = "Burton Green";

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.CreateOrUpdateAssetAsync(asset));
            //Assert
            Assert.Equal("The id of the asset must be set.", ex.Message);
        }

        [Fact]
        public async Task CreateAssetShouldCallCorrectUrlWithCorrectData()
        {
            //Arrange
            var asset = new ManagementAsset();
            asset.Title = new Dictionary<string, string>();
            asset.Title["en-US"] = "Burton Green";
            asset.SystemProperties = new SystemProperties();
            asset.SystemProperties.Id = "424";
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleAssetManagement.json");
            var requestUrl = "";
            var contentSet = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = async (HttpRequestMessage request) =>
            {
                contentSet = await (request.Content as StringContent).ReadAsStringAsync();
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };

            //Act
            var res = await _client.CreateOrUpdateAssetAsync(asset);
            //Assert
            Assert.Equal(HttpMethod.Put, requestMethod);
            Assert.Contains(@"""en-US"":""Burton Green""", contentSet);
            Assert.Equal("https://api.contentful.com/spaces/666/assets/424", requestUrl);
        }

        [Fact]
        public async Task CreateAssetShouldSetVersionHeaderCorrectly()
        {
            //Arrange
            var asset = new ManagementAsset();
            asset.Title = new Dictionary<string, string>();
            asset.Title["en-US"] = "Burton Green";
            asset.SystemProperties = new SystemProperties();
            asset.SystemProperties.Id = "424";
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleAssetManagement.json");
            var versionHeader = "";
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };

            //Act
            var res = await _client.CreateOrUpdateAssetAsync(asset, version: 234);
            //Assert
            Assert.Equal("234", versionHeader);
        }

        [Fact]
        public async Task GetAllPublishedAssetsShouldReturnCorrectCollection()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\AssetsCollectionManagement.json");

            //Act
            var res = await _client.GetPublishedAssetsCollectionAsync();

            //Assert
            Assert.Equal(7, res.Count());
            Assert.Equal(7, res.Total);
            Assert.Equal("Ernest Hemingway (1950)", res.First().Title["en-US"]);
            Assert.Equal(2290561, res.First().Files["en-US"].Details.Size);
            Assert.Equal(2940, res.First().Files["en-US"].Details.Image.Width);
            Assert.Equal("Alice in Wonderland", res.Last().Title["en-US"]);
        }

        [Fact]
        public async Task GetAssetShouldThrowIfNoIdSet()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleAssetManagement.json");

            //Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _client.GetAssetAsync(""));

            //Assert
            Assert.Equal("assetId", ex.Message);
        }

        [Fact]
        public async Task GetAssetShouldReturnCorrectItem()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleAssetManagement.json");

            //Act
            var res = await _client.GetAssetAsync("234");

            //Assert
            Assert.Equal("Ernest Hemingway (1950)", res.Title["en-US"]);
            Assert.Equal("Hemingway in the cabin of his boat Pilar, off the coast of Cuba", res.Description["en-US"]);
        }

        [Theory]
        [InlineData("7HG7")]
        [InlineData("asf")]
        [InlineData("666")]
        public async Task DeletingAnAssetShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = new HttpResponseMessage();
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };

            //Act
            await _client.DeleteAssetAsync(id, 86);

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal("86", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/assets/{id}", requestUrl);
        }

        [Theory]
        [InlineData("7HG7", "en-US")]
        [InlineData("asf", "sv")]
        [InlineData("g3445g", "cucumber")]
        public async Task ProcessingAnAssetShouldCallCorrectUrl(string id, string locale)
        {
            //Arrange
            _handler.Response = new HttpResponseMessage();
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };

            //Act
            await _client.ProcessAssetAsync(id, 25, locale);

            //Assert
            Assert.Equal(HttpMethod.Put, requestMethod);
            Assert.Equal("25", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/assets/{id}/files/{locale}/process", requestUrl);
        }

        [Theory]
        [InlineData("7HG7")]
        [InlineData("asf")]
        [InlineData("666")]
        public async Task PublishingAnAssetShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleAssetManagement.json");
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };

            //Act
            await _client.PublishAssetAsync(id, 23);

            //Assert
            Assert.Equal(HttpMethod.Put, requestMethod);
            Assert.Equal("23", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/assets/{id}/published", requestUrl);
        }

        [Theory]
        [InlineData("7HG7")]
        [InlineData("ab45")]
        [InlineData("666")]
        public async Task UnpublishingAnAssetShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleAssetManagement.json");
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };

            //Act
            await _client.UnpublishAssetAsync(id, 38);

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal("38", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/assets/{id}/published", requestUrl);
        }

        [Theory]
        [InlineData("asg")]
        [InlineData("435")]
        [InlineData("785685fgh")]
        public async Task ArchivingAnAssetShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleAssetManagement.json");
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };

            //Act
            await _client.ArchiveAssetAsync(id, 23);

            //Assert
            Assert.Equal(HttpMethod.Put, requestMethod);
            Assert.Equal("23", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/assets/{id}/archived", requestUrl);
        }

        [Theory]
        [InlineData("57y43hr")]
        [InlineData("346w")]
        [InlineData("babt3")]
        public async Task UnarchivingAnAssetShouldCallCorrectUrl(string id)
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleAssetManagement.json");
            var requestUrl = "";
            var versionHeader = "";
            var requestMethod = HttpMethod.Trace;
            _handler.VerifyRequest = (HttpRequestMessage request) =>
            {
                requestMethod = request.Method;
                requestUrl = request.RequestUri.ToString();
            };
            _handler.VerificationBeforeSend = () =>
            {
                versionHeader = _httpClient.DefaultRequestHeaders.GetValues("X-Contentful-Version").First();
            };

            //Act
            await _client.UnarchiveAssetAsync(id, 89);

            //Assert
            Assert.Equal(HttpMethod.Delete, requestMethod);
            Assert.Equal("89", versionHeader);
            Assert.Equal($"https://api.contentful.com/spaces/666/assets/{id}/archived", requestUrl);
        }

        [Fact]
        public async Task GetAllLocalesShouldReturnCorrectCollection()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\LocalesCollection.json");

            //Act
            var res = await _client.GetLocalesCollectionAsync();

            //Assert
            Assert.Equal(1, res.Count());
            Assert.Equal(1, res.Total);
            Assert.Equal("U.S. English", res.First().Name);
            Assert.Equal("en-US", res.First().Code);
            Assert.True(res.First().Default);
            Assert.Null(res.First().FallbackCode);
        }

        [Fact]
        public async Task WebHookCallDetailsShouldDeserializeCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\WebhookCallDetails.json");
            //Act
            var res = await _client.GetWebHookCallDetailsAsync("b", "s");

            //Assert
            Assert.Equal("unarchive", res.EventType);
            Assert.Equal("close", res.Response.Headers["connection"]);

        }

        [Fact]
        public void ConstraintsShouldSerializeCorrectly()
        {
            //Arrange
            var policy = new Policy();
            policy.Effect = "allow";
            policy.Actions = new List<string>()
            {
                "create",
                "delete"
            };
            policy.Constraint =
                new AndConstraint()
                {
                    new EqualsConstraint()
                    {
                        Property = "sys.type",
                        ValueToEqual = "Entry"
                    },
                    new EqualsConstraint()
                    {
                        Property = "sys.createdBy.sys.id",
                        ValueToEqual = "User.current()"
                    },
                    new NotConstraint()
                    {
                        ConstraintToInvert = new EqualsConstraint()
                        {
                            Property = "sys.contentType.sys.id",
                            ValueToEqual = "123"
                        }
                    }
            };

            //Act
            string output = JsonConvert.SerializeObject(policy, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });

            //Assert
            Assert.Equal(@"{""effect"":""allow"",""actions"":[""create"",""delete""],""constraint"":{""and"":[{""equals"":[{""doc"":""sys.type""},""Entry""]},{""equals"":[{""doc"":""sys.createdBy.sys.id""},""User.current()""]},{""not"":{""equals"":[{""doc"":""sys.contentType.sys.id""},""123""]}}]}}", output);
        }

        [Fact]
        public async Task RolesShouldDeserializeCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleRole.json");

            //Act
            var res = await _client.GetRoleAsync("123");

            //Assert
            Assert.Equal("Developer", res.Name);
            Assert.Equal("sys.type", ((res.Policies[1].Constraint as AndConstraint)[0] as EqualsConstraint).Property);
            Assert.Equal("Asset", ((res.Policies[1].Constraint as AndConstraint)[0] as EqualsConstraint).ValueToEqual);
        }

        [Fact]
        public async Task RoleShouldSerializeCorrectly()
        {
            //Arrange
            var role = new Role();
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleRole.json");


            role.Name = "test";
            role.Description = "desc";
            role.Permissions = new ContentfulPermissions();
            role.Permissions.ContentDelivery = new List<string>() { "all" };
            role.Permissions.ContentModel = new List<string>() { "read" };
            role.Permissions.Settings = new List<string>() { "read", "manage" };
            role.Policies = new List<Policy>();
            role.Policies.Add(new Policy()
            {
                Effect = "allow",
                Actions = new List<string>()
                {
                    "read",
                    "create",
                    "update"
                },
                Constraint = new AndConstraint()
                {
                    new EqualsConstraint()
                    {
                        Property = "sys.type",
                        ValueToEqual = "Entry"
                    }
                }
            });

            //Act
            var res = await _client.CreateRoleAsync(role);

            //Assert
            Assert.Equal("Developer", res.Name);
            Assert.Equal("sys.type", ((res.Policies[1].Constraint as AndConstraint)[0] as EqualsConstraint).Property);
            Assert.Equal("Asset", ((res.Policies[1].Constraint as AndConstraint)[0] as EqualsConstraint).ValueToEqual);
        }

        [Fact]
        public async Task RolesCollectionShouldDeserializeCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleRolesCollection.json");

            //Act
            var res = await _client.GetAllRolesAsync();

            //Assert
            Assert.Equal(7, res.Total);
            Assert.Equal("Author", res.First().Name);
            Assert.Equal("create", res.First().Policies.First().Actions[0]);
            Assert.Equal("Translator 1", res.ElementAt(4).Name);
            Assert.Equal("Entry", ((res.ElementAt(4).Policies.First().Constraint as AndConstraint).First() as EqualsConstraint).ValueToEqual);

        }

        [Fact]
        public async Task SnapshotsShouldDeserializeCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\SnapshotsCollection.json");

            //Act
            var res = await _client.GetAllSnapshotsForEntryAsync("123");

            //Assert
            Assert.Equal("Seven Tips From Ernest Hemingway on How to Write Fiction", res.First().Fields["title"]["en-US"]);
        }

        [Fact]
        public async Task SpaceMembershipsShouldDeserializeCorrectly()
        {
            //Arrange
            _handler.Response = GetResponseFromFile(@"JsonFiles\SampleSpaceMembershipsCollection.json");

            //Act
            var res = await _client.GetSpaceMembershipsAsync();

            //Assert
            Assert.Equal(1, res.Total);
            Assert.True(res.First().Admin);
            Assert.Equal("123", res.First().Roles[1]);
        }

        [Fact]
        public async Task SpaceMembershipShouldSerializeCorrectly()
        {
            //Arrange
            var membership = new SpaceMembership();
            membership.Admin = true;
            membership.Roles = new List<string>()
            {
                "123",
                "231",
                "12344"
            };
            var serializedMembership = JsonConvert.SerializeObject(membership);
            _handler.Response = new HttpResponseMessage() { Content = new StringContent(serializedMembership) };

            //Act
            var res = await _client.CreateSpaceMembershipAsync(membership);

            //Assert
            Assert.True(res.Admin);
            Assert.Equal(3, res.Roles.Count);
            Assert.Equal("231", res.Roles[1]);
        }

        private HttpResponseMessage GetResponseFromFile(string file)
        {
            //So, this is an ugly hack... Any better way to get the absolute path of the test project?
            var projectPath = Directory.GetParent(typeof(Asset).GetTypeInfo().Assembly.Location).Parent.Parent.Parent.FullName;
            var response = new HttpResponseMessage();
            var fullPath = Path.Combine(projectPath, file);
            response.Content = new StringContent(System.IO.File.ReadAllText(fullPath));
            return response;
        }
    }
}
