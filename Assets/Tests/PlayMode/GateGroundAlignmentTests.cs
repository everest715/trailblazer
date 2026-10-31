using System;
using System.Linq;
using NUnit.Framework;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class GateGroundAlignmentTests
{
    [Test]
    public void EnableGate_UsesDetectedGroundHeight()
    {
        var gameType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .First(type => type.Name == "Game");
        var car = new GameObject("Car");
        car.AddComponent<AudioSource>();
        var game = car.AddComponent(gameType);
        var serialized = new SerializedObject(game);
        serialized.FindProperty("groundHeight").floatValue = 0.2f;
        serialized.FindProperty("activeGate").objectReferenceValue = new GameObject("GatePrefab");
        var original = new GameObject("OldGate");
        original.transform.position = new Vector3(1f, 2f, 3f);

        var method = gameType.GetMethod("EnableGate", BindingFlags.Instance | BindingFlags.NonPublic);
        var gate = (GameObject)method.Invoke(game, new object[] { original, false, false });

        Assert.AreEqual(0.2f, gate.transform.position.y);
        UnityEngine.Object.DestroyImmediate(gate);
        UnityEngine.Object.DestroyImmediate(original);
        UnityEngine.Object.DestroyImmediate(car);
    }
}
