using System;
using System.Collections.Generic;
using System.Linq;
using AutoFixture.Xunit2;
using CluedIn.Core.Connectors;
using CluedIn.Core.Data.Vocabularies;
using Xunit;

namespace CluedIn.Connector.AzureServiceBus.Unit.Tests
{
    public class AzureServiceBusGenerationTests : AzureServiceBusConnectorTestsBase
    {
        [Theory, InlineAutoData]
        public void EmptyContainerWorks(string name)
        {
            //var result = Sut.BuildEmptyContainerSql(name);

            //Assert.Equal($"TRUNCATE TABLE [{name}]", result.Trim());
        }

        // [Theory, InlineAutoData]
        // public void CreateContainerWorks(string name)
        // {
        //     var model = new CreateContainerModel
        //     {
        //         Name = name,
        //         DataTypes = new List<ConnectionDataType>
        //         {
        //             new ConnectionDataType { Name = "Field1", Type = VocabularyKeyDataType.Integer },
        //             new ConnectionDataType { Name = "Field2", Type = VocabularyKeyDataType.Text },
        //             new ConnectionDataType { Name = "Field3", Type = VocabularyKeyDataType.DateTime },
        //             new ConnectionDataType { Name = "Field4", Type = VocabularyKeyDataType.Number },
        //             new ConnectionDataType { Name = "Field5", Type = VocabularyKeyDataType.Boolean },
        //         }
        //     };
        //
        //     var result = Sut.BuildCreateContainerSql(model.Name, model.DataTypes);
        //
        //     Assert.Equal($"CREATE TABLE [{name}]( [Field1] bigint NULL, [Field2] nvarchar(max) NULL, [Field3] datetime2 NULL, [Field4] decimal(18,4) NULL, [Field5] nvarchar(max) NULL) ON[PRIMARY]", result.Trim().Replace(Environment.NewLine, " "));
        // }

        [Theory, InlineAutoData]
        public void StoreDataWorks(string name, int field1, string field2, DateTime field3, decimal field4, bool field5)
        {
            var data = new Dictionary<string, object>
                        {
                             { "Field1", field1   },
                             { "Field2", field2   },
                             { "Field3", field3  },
                             { "Field4", field4   },
                             { "Field5", field5   }
                        };

            //var result = Sut.BuildStoreDataSql(name, data, out var param);

            //Assert.Equal($"MERGE [{name}] AS target" + Environment.NewLine +
            //             "USING (SELECT @Field1, @Field2, @Field3, @Field4, @Field5) AS source ([Field1], [Field2], [Field3], [Field4], [Field5])" + Environment.NewLine +
            //             "  ON (target.[OriginEntityCode] = source.[OriginEntityCode])" + Environment.NewLine +
            //             "WHEN MATCHED THEN" + Environment.NewLine +
            //             "  UPDATE SET target.[Field1] = source.[Field1], target.[Field2] = source.[Field2], target.[Field3] = source.[Field3], target.[Field4] = source.[Field4], target.[Field5] = source.[Field5]" + Environment.NewLine +
            //             "WHEN NOT MATCHED THEN" + Environment.NewLine +
            //             "  INSERT ([Field1], [Field2], [Field3], [Field4], [Field5])" + Environment.NewLine +
            //             "  VALUES (source.[Field1], source.[Field2], source.[Field3], source.[Field4], source.[Field5]);", result.Trim());
            //Assert.Equal(data.Count, param.Count);

            //for (var index = 0; index < data.Count; index++)
            //{
            //    var parameter = param[index];
            //    var val = data[$"Field{index + 1}"];
            //    Assert.Equal(val, parameter.Value);
            //}
        }

        [Theory, InlineAutoData]
        public void StoreEdgeDataWorks(string name, string originEntityCode, List<string> edges)
        {
            //var result = Sut.BuildEdgeStoreDataSql(name, originEntityCode, edges, out var param);
            //Assert.Equal(edges.Count + 1, param.Count); // params will also include origin entity code
            //Assert.Contains(param, p => p.ParameterName == "@OriginEntityCode" && p.Value.Equals(originEntityCode));
            //for(var index = 0; index < edges.Count; index++)
            //{
            //    Assert.Contains(param, p => p.ParameterName == $"@{index}" && p.Value.Equals(edges[index]));
            //}

            //var expectedLines = new List<string>
            //{
            //    $"DELETE FROM [{name}] where [OriginEntityCode] = @OriginEntityCode",
            //    $"INSERT INTO [{name}] ([OriginEntityCode],[Code]) values",
            //    string.Join(", ", Enumerable.Range(0, edges.Count).Select(i => $"(@OriginEntityCode, @{i})"))
            //};

            //var expectedSql = string.Join(Environment.NewLine, expectedLines);
            //Assert.Equal(expectedSql, result.Trim());
        }

        [Theory, InlineAutoData]
        public void StoreEdgeData_NoEdges_Works(string name, string originEntityCode)
        {
            var edges = new List<string>();
            //var result = Sut.BuildEdgeStoreDataSql(name, originEntityCode, edges, out var param);
            //Assert.Single(param); // params will also include origin entity code
            //Assert.Contains(param, p => p.ParameterName == "@OriginEntityCode" && p.Value.Equals(originEntityCode));
            //Assert.Equal($"DELETE FROM [{name}] where [OriginEntityCode] = @OriginEntityCode", result.Trim());
        }
    }
}
