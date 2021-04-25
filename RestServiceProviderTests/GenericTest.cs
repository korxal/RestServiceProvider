using NUnit.Framework;

namespace RestServiceProviderTests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            var Instance = new TestService();
            RestServiceProvider.RestProvider provider = new RestServiceProvider.RestProvider();
            Assert.DoesNotThrow(()=>provider.Start());
            Assert.DoesNotThrow(() => provider.RegisterApi(Instance));




            Assert.Pass();
        }
    }
}