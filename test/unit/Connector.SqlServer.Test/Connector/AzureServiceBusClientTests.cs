using System;
using System.Collections.Generic;
using System.Text;
using CluedIn.Connector.AzureServiceBus.Connector;
using Xunit;

namespace CluedIn.Connector.AzureServiceBus.Unit.Tests.Connector
{
    public class HttpClientTests
    {
        private readonly AzureServiceBusClient _sut;

        public HttpClientTests()
        {
            _sut = new AzureServiceBusClient();
        }

        //[Fact]
        //public void BuildConnectionString_Sets_From_Dictionary()
        //{
        //    var properties = new Dictionary<string, object>
        //    {
        //        [HttpConstants.KeyName.Password] = "password",
        //        [HttpConstants.KeyName.Username] = "user",
        //        [HttpConstants.KeyName.Url] = "host",
        //        [HttpConstants.KeyName.DatabaseName] = "database"
        //    };

        //    var result = _sut.BuildConnectionString(properties);

        //    Assert.Equal("Data Source=host;Initial Catalog=database;User ID=user;Password=password;Authentication=SqlPassword", result);
        //}

        //[Fact]
        //public void BuildConnectionString_WithPort_Sets_From_Dictionary()
        //{
        //    var properties = new Dictionary<string, object>
        //    {
        //        [HttpConstants.KeyName.Password] = "password",
        //        [HttpConstants.KeyName.Username] = "user",
        //        [HttpConstants.KeyName.Host] = "host",
        //        [HttpConstants.KeyName.DatabaseName] = "database",
        //        [HttpConstants.KeyName.PortNumber] = 9499,
        //    };

        //    var result = _sut.BuildConnectionString(properties);

        //    Assert.Equal("Data Source=host,9499;Initial Catalog=database;User ID=user;Password=password;Authentication=SqlPassword", result);
        //}

        //[Fact]
        //public void BuildConnectionString_WithStringPort_Sets_From_Dictionary()
        //{
        //    var properties = new Dictionary<string, object>
        //    {
        //        [HttpConstants.KeyName.Password] = "password",
        //        [HttpConstants.KeyName.Username] = "user",
        //        [HttpConstants.KeyName.Host] = "host",
        //        [HttpConstants.KeyName.DatabaseName] = "database",
        //        [HttpConstants.KeyName.PortNumber] = "9499",
        //    };

        //    var result = _sut.BuildConnectionString(properties);

        //    Assert.Equal("Data Source=host,9499;Initial Catalog=database;User ID=user;Password=password;Authentication=SqlPassword", result);
        //}

        //[Fact]
        //public void BuildConnectionString_WithInvalidPort_Sets_From_Dictionary()
        //{
        //    var properties = new Dictionary<string, object>
        //    {
        //        [HttpConstants.KeyName.Password] = "password",
        //        [HttpConstants.KeyName.Username] = "user",
        //        [HttpConstants.KeyName.Host] = "host",
        //        [HttpConstants.KeyName.DatabaseName] = "database",
        //        [HttpConstants.KeyName.PortNumber] = new object(),
        //    };

        //    var result = _sut.BuildConnectionString(properties);

        //    Assert.Equal("Data Source=host;Initial Catalog=database;User ID=user;Password=password;Authentication=SqlPassword", result);
        //}
    }
}
