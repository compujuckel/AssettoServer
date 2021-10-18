using System.Collections.Generic;
using System.Numerics;
using AssettoServer.Server;
using NUnit.Framework;

namespace AssettoServerTest
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void GetBestStateForPlayer_Test()
        {
            List<CarStatus> candidates = new List<CarStatus>()
            {
                new CarStatus()
                {
                    Position = new Vector3(200, 0, 200),
                    Velocity = new Vector3(2, 0, 1)
                },
                new CarStatus()
                {
                    Position = new Vector3(-300, 0, 300),
                    Velocity = new Vector3(-1, 0, -1)
                },
                new CarStatus()
                {
                    Position = new Vector3(50, 0, 50),
                    Velocity = new Vector3(0.3f, 0, 1)
                },
                new CarStatus()
                {
                    Position = new Vector3(-50, 10, -10),
                    Velocity = new Vector3(-0.3f, 1, 1)
                },
                new CarStatus()
                {
                    Position = new Vector3(10, -10, 10),
                    Velocity = new Vector3(-1f, -1, -1)
                },
            };

            CarStatus target = new CarStatus()
            {
                Position = new Vector3(0, 0, 0),
                Velocity = new Vector3(0, 1, 3)
            };

            var ret = EntryCar.GetBestStateForPlayer(target, candidates, 250 * 250);

            Assert.AreEqual(candidates[3], ret);
        }
    }
}