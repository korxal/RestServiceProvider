using System;
using System.Collections.Generic;
using System.Text;

namespace ExampleService
{
    public class Trade
    {
        public decimal Volume;
        public float Qty;
        public Guid TradeGuid;
        public int TradeType;
        public DateTime TradeDate;
        public bool IsOtc;
        public Instrument instrument;
        public List<Counterparty> Sides;
        public Dictionary<int, Counterparty> SidesDict;
        public List<Dictionary<string, Counterparty>> Nightmare;

        public int AngryProp { get; set; }
    }

    public class Instrument
    {
        public decimal FaceValue;
        public string Symbol;
    }


    public class Counterparty
    {
        public string Name;
    }

    public class TestService
    {

        public Trade GetTrade(DateTime TradeDate)
        {

            return new Trade
            {
                IsOtc = true,
                Qty = 11.1f,
                TradeDate = TradeDate,
                TradeType = 1,
                TradeGuid = Guid.NewGuid(),
                Volume = 6666.6m,
                instrument = new Instrument() { Symbol = "KRX", FaceValue = 1000 },
                SidesDict = new Dictionary<int, Counterparty>()
                {
                    { 1, new Counterparty() { Name = "VTBC" } },
                    { 2, new Counterparty() { Name = "SBERP" } }
                },

                Sides = new List<Counterparty>()
                {
                    new Counterparty() { Name = "ABD" },
                    new Counterparty() { Name = "CBOM" }
                },
                AngryProp = 42,
                Nightmare = new List<Dictionary<string, Counterparty>>()
                {
                    new Dictionary<string, Counterparty>()
                    {
                        { "1", new Counterparty() { Name = "GAZP" } },
                        { "2", new Counterparty() { Name = "GAZR" } }
                    }

                      ,
                    new Dictionary<string, Counterparty>()
                    {
                        { "3", new Counterparty() { Name = "SGNX" } },
                        { "4", new Counterparty() { Name = "SGNP" } }
                    }

                }

            };


        }

        public Instrument Instrument(string Symbol)
        {
            return new Instrument() { Symbol = Symbol, FaceValue = 1000 };
        }

        public Instrument Instrument(Instrument i)
        {
            return new Instrument() { Symbol = i.Symbol, FaceValue = i.FaceValue };
        }

    }

}
