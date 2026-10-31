using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GateGroundAlignmentTests
{
    [Test]
    public void EnableGate_UsesDetectedGroundHeight()
    {
        var car = new GameObject("Car");
        car.AddComponent<AudioSource>();
        var game = car.AddComponent<Game>();
        game.groundHeight = 0.2f;
        game.activeGate = new GameObject("GatePrefab");
        var original = new GameObject("OldGate");
        original.transform.position = new Vector3(1f, 2f, 3f);

        var method = typeof(Game).GetMethod("EnableGate", BindingFlags.Instance | BindingFlags.NonPublic);
        var gate = (GameObject)method.Invoke(game, new object[] { original, false, false });

        Assert.AreEqual(0.2f, gate.transform.position.y);
        Object.DestroyImmediate(gate);
        Object.DestroyImmediate(original);
        Object.DestroyImmediate(car);
    }
}
