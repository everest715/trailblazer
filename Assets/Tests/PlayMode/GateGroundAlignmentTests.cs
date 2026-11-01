using System;
using System.Linq;
using NUnit.Framework;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class GateGroundAlignmentTests
{
    [Test]
    public void AlignGatesToHeadset_MovesGateRootInFrontOfCamera()
    {
        var gameType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .First(type => type.Name == "Game");
        var car = new GameObject("Car");
        car.AddComponent<AudioSource>();
        var game = car.AddComponent(gameType);
        var gatesRoot = new GameObject("Gates");
        gatesRoot.transform.position = new Vector3(0f, 0f, 1.5f);
        var gate = new GameObject("Gate1");
        gate.transform.SetParent(gatesRoot.transform);
        var serialized = new SerializedObject(game);
        serialized.FindProperty("gate1").objectReferenceValue = gate;
        var cameraObject = new GameObject("Headset");
        cameraObject.tag = "MainCamera";
        cameraObject.AddComponent<Camera>();
        cameraObject.transform.SetPositionAndRotation(
            new Vector3(1f, 1.6f, 2f), Quaternion.Euler(0f, 90f, 0f));

        var method = gameType.GetMethod("AlignGatesToHeadset", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Invoke(game, null);

        Assert.Less(Vector3.Distance(new Vector3(2.5f, 0f, 2f), gatesRoot.transform.position), 0.0001f);
        Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 90f, 0f), gatesRoot.transform.rotation), 0.01f);
        Assert.AreEqual(gatesRoot.transform, gate.transform.parent);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        UnityEngine.Object.DestroyImmediate(gatesRoot);
        UnityEngine.Object.DestroyImmediate(car);
    }

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
