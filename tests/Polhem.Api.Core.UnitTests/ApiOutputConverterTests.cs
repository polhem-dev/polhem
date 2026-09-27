using System.ComponentModel;
using System.Data;
using System.Text.Json;
using Polhem.Api.Core.JsonRpc;
using Polhem.Api.Core.Messages.Form;
using Polhem.Api.Core.Messages.System;
using Polhem.Base.Serialization;
using Polhem.Business.System;
using Polhem.Api.Core.Conversion;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Tests for ApiOutputConverter.Convert and ConvertResultValue.
    /// </summary>
    public class ApiOutputConverterTests
    {
        [Fact]
        [DisplayName("Convert returns null for null")]
        public void Convert_Null_ReturnsNull()
        {
            var result = ApiOutputConverter.Convert(null!);
            Assert.Null(result);
        }

        [Fact]
        [DisplayName("Convert turns a BO Result into the matching API Response")]
        public void Convert_BoResult_ReturnsApiResponse()
        {
            var boResult = new PingResult
            {
                Status = "ok",
                Version = "9.9.9",
                TraceId = "TRACE-1"
            };

            var converted = ApiOutputConverter.Convert(boResult);

            var response = Assert.IsType<PingResponse>(converted);
            Assert.Equal("ok", response.Status);
            Assert.Equal("9.9.9", response.Version);
            Assert.Equal("TRACE-1", response.TraceId);
        }

        [Fact]
        [DisplayName("Convert returns the original object when its type name has no Result suffix")]
        public void Convert_NonResultSuffix_ReturnsOriginal()
        {
            var input = "hello";
            var result = ApiOutputConverter.Convert(input);
            Assert.Same(input, result);
        }

        [Fact]
        [DisplayName("ConvertResultValue returns the value directly when it is already the target type")]
        public void ConvertResultValue_DirectType_ReturnsSame()
        {
            var original = new PingResponse { Status = "x" };
            var result = ApiOutputConverter.ConvertResultValue<PingResponse>(original);

            Assert.Same(original, result);
        }

        [Fact]
        [DisplayName("ConvertResultValue deserializes a JsonElement into the target type")]
        public void ConvertResultValue_JsonElement_Deserializes()
        {
            var json = """{"status":"ok","traceId":"T1"}""";
            using var doc = JsonDocument.Parse(json);
            var element = doc.RootElement.Clone();

            var result = ApiOutputConverter.ConvertResultValue<PingResponse>(element);

            Assert.NotNull(result);
            Assert.Equal("ok", result!.Status);
            Assert.Equal("T1", result.TraceId);
        }

        [Fact]
        [DisplayName("ConvertResultValue casts a compatible reference type")]
        public void ConvertResultValue_CastablePath_ReturnsCast()
        {
            object value = "hello";
            var result = ApiOutputConverter.ConvertResultValue<string>(value);
            Assert.Equal("hello", result);
        }
        [Fact]
        [DisplayName("ConvertResultValue keeps every row of a DataTable in a Plain list response")]
        public void ConvertResultValue_PlainGetListResponse_KeepsTableRows()
        {
            var table = new DataTable("Employee");
            table.Columns.Add("sys_id", typeof(string));
            table.Rows.Add("E001");
            table.Rows.Add("E002");
            table.AcceptChanges();
            var response = new JsonRpcResponse
            {
                Id = "1",
                Result = new JsonRpcResult { Value = new GetListResponse { Table = table } },
            };

            // The Plain wire: the server writes the response with `JsonCodec`, the client reads it back and converts
            // the value, which arrives as a `JsonElement`.
            var received = JsonCodec.Deserialize<JsonRpcResponse>(JsonCodec.Serialize(response))!;
            var result = ApiOutputConverter.ConvertResultValue<GetListResponse>(received.Result!.Value!);

            Assert.NotNull(result?.Table);
            Assert.Equal(2, result!.Table!.Rows.Count);
            Assert.Equal("E002", result.Table.Rows[1]["sys_id"]);
        }
    }
}
