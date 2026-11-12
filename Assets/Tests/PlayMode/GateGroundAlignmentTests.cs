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
        serialized.ApplyModifiedProperties();
        Assert.IsNotNull(Camera.main);
        Assert.AreEqual(gate, gameType.GetField("gate1").GetValue(game));

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
    public void AlignGatesToArea_FitsFormationInsideSurface()
    {
        var gameType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .First(type => type.Name == "Game");
        var car = new GameObject("Car");
        car.AddComponent<AudioSource>();
        var game = car.AddComponent(gameType);
        var gatesRoot = new GameObject("Gates");
        var gates = new[]
        {
            new GameObject("Gate1"), new GameObject("Gate2"),
            new GameObject("Gate3"), new GameObject("Gate4")
        };
        var positions = new[]
        {
            new Vector3(-4f, 0f, -2f), new Vector3(4f, 0f, -2f),
            new Vector3(4f, 0f, 2f), new Vector3(-4f, 0f, 2f)
        };
        var serialized = new SerializedObject(game);
        for (var i = 0; i < gates.Length; i++)
        {
            gates[i].transform.SetParent(gatesRoot.transform);
            gates[i].transform.position = positions[i];
            serialized.FindProperty($"gate{i + 1}").objectReferenceValue = gates[i];
        }
        var area = new PicoFloorArea(new[]
        {
            new Vector3(8f, 0.75f, 19f), new Vector3(12f, 0.75f, 19f),
            new Vector3(12f, 0.75f, 21f), new Vector3(8f, 0.75f, 21f)
        });
        serialized.ApplyModifiedProperties();

        var method = gameType.GetMethod("AlignGatesToArea", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Invoke(game, new object[] { area });

        foreach (var gate in gates)
        {
            Assert.IsTrue(area.ContainsXZ(gate.transform.position));
            Assert.AreEqual(area.SurfaceHeight + 0.1f, gate.transform.position.y, 1e-5f);
        }
        Assert.AreEqual(10f, gatesRoot.transform.position.x, 1e-5f);
        Assert.AreEqual(20f, gatesRoot.transform.position.z, 1e-5f);
        Assert.AreEqual(0.85f, (float)gameType.GetField("groundHeight").GetValue(game), 1e-5f);
        Assert.Less(Vector3.Distance(gates[0].transform.position, gates[1].transform.position), 8f);
        UnityEngine.Object.DestroyImmediate(gatesRoot);
        UnityEngine.Object.DestroyImmediate(car);
    }

    [Test]
    public void AlignGatesToArea_ZeroWidthFormationUsesAreaCenter()
    {
        var gameType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .First(type => type.Name == "Game");
        var car = new GameObject("Car");
        car.AddComponent<AudioSource>();
        var game = car.AddComponent(gameType);
        var gatesRoot = new GameObject("Gates");
        var gates = new[]
        {
            new GameObject("Gate1"), new GameObject("Gate2"),
            new GameObject("Gate3"), new GameObject("Gate4")
        };
        var serialized = new SerializedObject(game);
        for (var i = 0; i < gates.Length; i++)
        {
            gates[i].transform.SetParent(gatesRoot.transform);
            gates[i].transform.position = Vector3.zero;
            serialized.FindProperty($"gate{i + 1}").objectReferenceValue = gates[i];
        }
        var area = new PicoFloorArea(new[]
        {
            new Vector3(-1f, 0.2f, -1f), new Vector3(1f, 0.2f, -1f),
            new Vector3(1f, 0.2f, 1f), new Vector3(-1f, 0.2f, 1f)
        });
        serialized.ApplyModifiedProperties();

        var method = gameType.GetMethod("AlignGatesToArea", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Invoke(game, new object[] { area });

        foreach (var gate in gates)
        {
            Assert.IsFalse(float.IsNaN(gate.transform.position.x));
            Assert.AreEqual(new Vector3(0f, 0.3f, 0f), gate.transform.position);
        }
        UnityEngine.Object.DestroyImmediate(gatesRoot);
        UnityEngine.Object.DestroyImmediate(car);
    }

    [Test]
    public void AlignGatesToArea_ConcaveSurfaceKeepsAllGateCentersInside()
    {
        var gameType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .First(type => type.Name == "Game");
        var car = new GameObject("Car");
        car.AddComponent<AudioSource>();
        var game = car.AddComponent(gameType);
        var gatesRoot = new GameObject("Gates");
        var gates = new[]
        {
            new GameObject("Gate1"), new GameObject("Gate2"),
            new GameObject("Gate3"), new GameObject("Gate4")
        };
        var serialized = new SerializedObject(game);
        for (var i = 0; i < gates.Length; i++)
        {
            gates[i].transform.SetParent(gatesRoot.transform);
            gates[i].transform.position = new Vector3(i * 2f, 0f, 0f);
            serialized.FindProperty($"gate{i + 1}").objectReferenceValue = gates[i];
        }
        serialized.ApplyModifiedProperties();
        var area = new PicoFloorArea(new[]
        {
            new Vector3(-3f, 0f, -3f), new Vector3(0f, 0f, -3f),
            new Vector3(0f, 0f, 0f), new Vector3(3f, 0f, 0f),
            new Vector3(3f, 0f, 3f), new Vector3(-3f, 0f, 3f)
        });

        var method = gameType.GetMethod("AlignGatesToArea", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Invoke(game, new object[] { area });

        foreach (var gate in gates)
        {
            Assert.IsTrue(area.ContainsXZ(gate.transform.position));
        }
        UnityEngine.Object.DestroyImmediate(gatesRoot);
        UnityEngine.Object.DestroyImmediate(car);
    }

    [Test]
    public void EnableGate_PreservesWorldScaleWithoutChangingParent()
    {
        var gameType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .First(type => type.Name == "Game");
        var car = new GameObject("Car");
        car.AddComponent<AudioSource>();
        var game = car.AddComponent(gameType);
        var parent = new GameObject("Gates");
        parent.transform.localScale = Vector3.one * 0.25f;
        var original = new GameObject("OldGate");
        original.transform.SetParent(parent.transform, false);
        var prefab = new GameObject("GatePrefab");
        prefab.transform.localScale = Vector3.one * 2f;
        var serialized = new SerializedObject(game);
        serialized.FindProperty("activeGate").objectReferenceValue = prefab;
        serialized.ApplyModifiedProperties();
        gameType.GetField("_audioSource", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(game, car.GetComponent<AudioSource>());

        var method = gameType.GetMethod("EnableGate", BindingFlags.Instance | BindingFlags.NonPublic);
        var gate = (GameObject)method.Invoke(game, new object[] { original, false, false });

        Assert.IsNull(gate.transform.parent);
        Assert.AreEqual(Vector3.one * 0.25f, gate.transform.lossyScale);
        UnityEngine.Object.DestroyImmediate(gate);
        UnityEngine.Object.DestroyImmediate(original);
        UnityEngine.Object.DestroyImmediate(prefab);
        UnityEngine.Object.DestroyImmediate(parent);
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
        serialized.ApplyModifiedProperties();
        gameType.GetField("_audioSource", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(game, car.GetComponent<AudioSource>());
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
