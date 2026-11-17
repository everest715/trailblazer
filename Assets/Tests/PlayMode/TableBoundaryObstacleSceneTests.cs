using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class TableBoundaryObstacleSceneTests
{
    [UnityTest]
    public IEnumerator BikeScene_ConfiguresTableBoundaryObstacles()
    {
        LogAssert.ignoreFailingMessages = true;
        SceneManager.LoadScene("BikeScene", LoadSceneMode.Single);
        yield return null;
        var scene = SceneManager.GetActiveScene();
        try
        {
            var generator = Object.FindObjectsOfType<PicoTableBoundaryObstacles>(true).SingleOrDefault();
            Assert.NotNull(generator);
            var provider = generator.sceneCaptureProvider;
            Assert.IsInstanceOf<IPicoFloorAreaProvider>(provider);
            Assert.AreEqual("PICO", provider.gameObject.name);
            Assert.AreEqual("MFence", generator.fencePrefab == null ? null : generator.fencePrefab.name);
            Assert.AreEqual("MTree", generator.treePrefab == null ? null : generator.treePrefab.name);
            Assert.AreEqual("MTree2", generator.tree2Prefab == null ? null : generator.tree2Prefab.name);
            Assert.AreEqual(8f, generator.fenceMaxYaw);
            var serializedProvider = new SerializedObject(provider);
            Assert.AreEqual(7, serializedProvider.FindProperty("surfaceLabel").intValue);
            Assert.IsFalse(serializedProvider.FindProperty("useTrackerForTableHeight").boolValue);
            Assert.IsTrue(serializedProvider.FindProperty("showDebugMeshes").boolValue);
        }
        finally
        {
            var emptyScene = SceneManager.CreateScene("TableBoundarySceneTestCleanup");
            SceneManager.SetActiveScene(emptyScene);
            SceneManager.UnloadSceneAsync(scene);
            LogAssert.ignoreFailingMessages = false;
        }
        yield return null;
    }
}
