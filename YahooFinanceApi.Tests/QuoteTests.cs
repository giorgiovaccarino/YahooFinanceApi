using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Flurl.Http;
using Xunit;
using Xunit.Abstractions;

namespace YahooFinanceApi.Tests
{
    public class Quotes
    {
        protected readonly Action<string> Write;
        public Quotes(ITestOutputHelper output) => Write = output.WriteLine;

        static Quotes() // static ctor
        {
            // Test culture
            CultureInfo.CurrentCulture = new CultureInfo("nl-nl");

            // may speed up http when implemented by application
            ServicePointManager.UseNagleAlgorithm = false;
            ServicePointManager.DefaultConnectionLimit = 100;
        }

        [Fact]
        public async Task TestQueryArguments()
        {
            // no symbols
            await Assert.ThrowsAsync<ArgumentException>(async () => await Yahoo.Symbols().QueryAsync());

            // duplicate symbol
            await Assert.ThrowsAsync<ArgumentException>(async () => await Yahoo.Symbols("C", "A", "C").QueryAsync());

            // note that invalid fields have no effect!
            await Yahoo.Symbols("C").Fields("invalidfield").QueryAsync();

            // duplicate field
            await Assert.ThrowsAsync<ArgumentException>(async () => 
                await Yahoo.Symbols("C").Fields("currency", "bid").Fields(Field.Ask, Field.Bid).QueryAsync());

            // when no fields are specified, all fields are returned, it seems
            await Yahoo.Symbols("C").QueryAsync();
        }

        [Fact]
        public async Task TestInvalidSymbol()
        {
            // invalid symbols are ignored by Yahoo!

            var result = await Yahoo.Symbols("invalidsymbol").QueryAsync();
            Assert.Empty(result);

            result = await Yahoo.Symbols("C", "invalidsymbol", "X").QueryAsync();
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task TestQuery()
        {
            var securities = await Yahoo
                    .Symbols("C", "AAPL")
                    // Can use string field names ...
                    .Fields("Bid", "Ask", "Tradeable", "LongName")
                    // and/or Field enums.
                    .Fields(Field.RegularMarketPrice, Field.Currency)

                    .QueryAsync();

            Assert.Equal(2, securities.Count());

            double bid1 = securities["C"]["Bid"]; // strings => dynamic

            double bid2 = securities["C"][Field.Bid]; // Field enum => dynamic

            double bid3 = securities["C"].Bid; // property => static type


            Assert.True(securities["C"]["Tradeable"]);

            Assert.Equal("Apple Inc.", securities["AAPL"]["LongName"]);
        }

        [Fact]
        public async Task TestQueryNotRequested()
        {
            var securities = await Yahoo.Symbols("AAPL").Fields(Field.Symbol).QueryAsync();

            var security = securities.First().Value;

            // This field was requested and therefore will be available.
            Assert.Equal("AAPL", security.Symbol);

            // This field was not requested and is not available.
            Assert.Throws<KeyNotFoundException>(() => security.TwoHundredDayAverageChange);

            // Many fields are available even though only one was requested!
            Assert.True(security.Fields.Count > 1);
        }

        private async Task<List<KeyValuePair<string,dynamic>>> GetFields()
        {
            var securities = await Yahoo.Symbols("C").QueryAsync();
            return securities.Single()
                .Value
                .Fields
                .OrderBy(x => x.Key)
                .ToList();
        }

        [Fact]
        public async Task MakeEnumList()
        {
            var fields = await GetFields();

            Write("// Fields.cs enums. This list was generated automatically. These names have been defined by Yahoo.");
            Write(String.Join("," + Environment.NewLine, fields.Select(x => x.Key)));
            Write(Environment.NewLine);
        }

        [Fact]
        public async Task MakePropertyList()
        {
            var fields = await GetFields();

            Write("// Security.cs: This list was generated automatically. These names and types have been defined by Yahoo.");
            foreach (var field in fields)
                Write($"public {field.Value.GetType().Name} {field.Key} => this[\"{field.Key}\"];");
            Write(Environment.NewLine);
        }

    }
}
