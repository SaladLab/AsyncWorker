using Xunit;

namespace AsyncWorker.Tests
{
    public class WorkerTest
    {
        [Fact]
        public void PassingTest()
        {
            Assert.Equal(4, 2 + 2);
        }
    }
}
