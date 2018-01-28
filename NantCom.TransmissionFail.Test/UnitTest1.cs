using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NantCom.TransmissionFail.Test
{

    [TestClass]
    public class AllTest
    {
        [TestMethod]
        public void TestNewMatch()
        {
            GameMatch.NewMatch("testgame");
        }

        [TestMethod]
        public void TestListGame()
        {
            GameMatch.NewMatch("testgame2");

            var list = GameMatch.AllOpenMatch().ToList();
            Assert.IsTrue(list.Count > 0, "Some Game should be found" );

            var match = GameMatch.ById("testgame"); ;

            foreach (var item in GameMatch.AllOpenMatch())
            {
                Assert.IsTrue(item.GameId.StartsWith("testgame"), "Wrong Game Title");
            }
        }

        [TestMethod]
        public void TestImageSearch()
        {
            GameMatch.NewMatch("testgame");
            var match = GameMatch.ById("testgame");

            match.ImageSearch("monkey");

            Assert.IsTrue(match.Images.Count > 0, "Some image should be found");
        }


        [TestMethod]
        public void TestJoinGame()
        {
            GameMatch.NewMatch("testgame");
            var match = GameMatch.ById("testgame");

            match.ImageSearch("monkey");

            Assert.IsTrue(match.Images.Count > 0, "Some image should be found");
        }
    }
}
