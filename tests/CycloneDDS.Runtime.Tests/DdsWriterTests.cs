using System;
using Xunit;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;
using CycloneDDS.Runtime.Tests;

namespace CycloneDDS.Runtime.Tests
{
    [DdsTopic("BatchTestMessage")]
    [DdsQos(Reliability = DdsReliability.Reliable, BatchEnable = true, BatchMaxSamples = 3)]
    public partial struct BatchTestMessage
    {
        public int Id;
        public int Value;
    }

    public class DdsWriterTests
    {
        [Fact]
        public void CreateWriter_Success()
        {
            using var participant = new DdsParticipant(0);
            
            using var writer = new DdsWriter<TestMessage>(participant, "TestTopic_Unique1");
        }

        [Fact]
        public void Write_SingleSample_Success()
        {
            using var participant = new DdsParticipant(0);
            using var writer = new DdsWriter<TestMessage>(participant, "TestTopic_Unique2");
            
            var data = new TestMessage { Id = 1, Value = 123 };
            writer.Write(data);
        }
        
        [Fact]
        public void Dispose_Idempotent()
        {
            using var participant = new DdsParticipant(0);
            var writer = new DdsWriter<TestMessage>(participant, "TestTopic_Unique3");
            
            writer.Dispose();
            writer.Dispose();
        }

        [Fact]
        public void Write_AfterDispose_Throws()
        {
            using var participant = new DdsParticipant(0);
            var writer = new DdsWriter<TestMessage>(participant, "TestTopic_Unique4");
            
            writer.Dispose();
            
            Assert.Throws<ObjectDisposedException>(() => writer.Write(new TestMessage()));
        }

        [Fact]
        public void Batch_WriteAndFlush_Success()
        {
            using var participant = new DdsParticipant(0);
            using var writer = new DdsWriter<BatchTestMessage>(participant, "BatchTest_FlushSuccess");
            
            for (int i = 0; i < 5; i++)
                writer.Write(new BatchTestMessage { Id = i, Value = i * 10 });
            
            writer.Flush();
        }

        [Fact]
        public void Batch_FlushEmpty_DoesNotThrow()
        {
            using var participant = new DdsParticipant(0);
            using var writer = new DdsWriter<BatchTestMessage>(participant, "BatchTest_FlushEmpty");
            
            writer.Flush();
        }

        [Fact]
        public void Batch_FlushAfterDispose_Throws()
        {
            using var participant = new DdsParticipant(0);
            var writer = new DdsWriter<BatchTestMessage>(participant, "BatchTest_FlushAfterDispose");
            
            writer.Dispose();
            
            Assert.Throws<ObjectDisposedException>(() => writer.Flush());
        }

        [Fact]
        public void Batch_AutoFlushBySampleCount_DeliversToReader()
        {
            using var participant = new DdsParticipant(0);
            var topicName = "BatchTest_AutoFlush_" + Guid.NewGuid().ToString("N");
            using var writer = new DdsWriter<BatchTestMessage>(participant, topicName);
            using var reader = new DdsReader<BatchTestMessage>(participant, topicName);

            for (int i = 0; i < 3; i++)
                writer.Write(new BatchTestMessage { Id = i, Value = i * 10 });

            Assert.True(reader.WaitDataAsync().GetAwaiter().GetResult(), "Data should arrive after auto-flush");

            using var loan = reader.Take(maxSamples: 5);
            Assert.Equal(3, loan.Count);

            for (int i = 0; i < loan.Count; i++)
            {
                Assert.Equal(1, (int)loan.Infos[i].ValidData);
                Assert.Equal(i, loan[i].Id);
                Assert.Equal(i * 10, loan[i].Value);
            }
        }

        [Fact]
        public void Batch_ManualFlush_DeliversToReader()
        {
            using var participant = new DdsParticipant(0);
            var topicName = "BatchTest_ManualFlush_" + Guid.NewGuid().ToString("N");
            using var writer = new DdsWriter<BatchTestMessage>(participant, topicName);
            using var reader = new DdsReader<BatchTestMessage>(participant, topicName);

            writer.Write(new BatchTestMessage { Id = 42, Value = 99 });
            writer.Flush();

            Assert.True(reader.WaitDataAsync().GetAwaiter().GetResult(), "Data should arrive after manual flush");

            using var loan = reader.Take(maxSamples: 1);
            Assert.Equal(1, loan.Count);
            Assert.Equal(1, (int)loan.Infos[0].ValidData);
            Assert.Equal(42, loan[0].Id);
            Assert.Equal(99, loan[0].Value);
        }
    }
}
