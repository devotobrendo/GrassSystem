// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using System.Collections;
using UnityEngine;

namespace GrassSystem
{
    public class GrassStressTest : MonoBehaviour
    {
        [Header("Test Settings")]
        public int targetFPS = 30;
        public int grassPerStep = 1000;
        public float stabilizationTime = 2f;
        public float spawnAreaSize = 50f;
        
        [Header("References")]
        public GrassRenderer grassRenderer;
        public MeshFilter targetMesh;
        
        [Header("Test State")]
        [SerializeField] private bool isRunning;
        [SerializeField] private int currentGrassCount;
        [SerializeField] private float currentFPS;
        [SerializeField] private int maxSustainableCount;
        [SerializeField] private string testStatus = "Idle";
        
        private float[] fpsHistory = new float[30];
        private int fpsIndex;
        private float fpsSum;
        
        private void Start()
        {
            if (grassRenderer == null)
                grassRenderer = FindAnyObjectByType<GrassRenderer>();
        }
        
        private void Update()
        {
            float currentFrameFPS = 1f / Time.unscaledDeltaTime;
            fpsSum -= fpsHistory[fpsIndex];
            fpsHistory[fpsIndex] = currentFrameFPS;
            fpsSum += currentFrameFPS;
            fpsIndex = (fpsIndex + 1) % fpsHistory.Length;
            currentFPS = fpsSum / fpsHistory.Length;
        }
        
        [ContextMenu("Start Stress Test")]
        public void StartTest()
        {
            if (isRunning) return;
            if (grassRenderer == null)
            {
                Debug.LogError("GrassStressTest: No GrassRenderer assigned!");
                return;
            }
            StartCoroutine(RunStressTest());
        }
        
        [ContextMenu("Stop Test")]
        public void StopTest()
        {
            isRunning = false;
            StopAllCoroutines();
            testStatus = "Stopped";
        }
        
        [ContextMenu("Reset")]
        public void ResetTest()
        {
            StopTest();
            if (grassRenderer != null)
                grassRenderer.ClearGrass();
            currentGrassCount = 0;
            maxSustainableCount = 0;
            testStatus = "Reset";
        }
        
        private IEnumerator RunStressTest()
        {
            isRunning = true;
            testStatus = "Initializing...";
            
            grassRenderer.ClearGrass();
            currentGrassCount = 0;
            maxSustainableCount = 0;
            
            yield return new WaitForSeconds(1f);
            
            testStatus = "Running stress test...";
            Debug.Log("=== GRASS STRESS TEST STARTED ===");
            Debug.Log($"Target FPS: {targetFPS}");
            Debug.Log($"Grass per step: {grassPerStep}");
            
            while (isRunning)
            {
                AddGrassBatch(grassPerStep);
                currentGrassCount = grassRenderer.GrassDataList.Count;
                testStatus = $"Testing {currentGrassCount:N0} grass...";
                
                grassRenderer.RebuildBuffers();
                yield return new WaitForSeconds(stabilizationTime);
                
                Debug.Log($"Grass: {currentGrassCount:N0} | FPS: {currentFPS:F1}");
                
                if (currentFPS >= targetFPS)
                    maxSustainableCount = currentGrassCount;
                else
                {
                    testStatus = "Test complete!";
                    isRunning = false;
                    break;
                }
                
                if (currentGrassCount > 500000)
                {
                    testStatus = "Reached safety limit (500k)";
                    maxSustainableCount = currentGrassCount;
                    isRunning = false;
                    break;
                }
            }
            
            OutputResults();
        }
        
        private void AddGrassBatch(int count)
        {
            var grassList = grassRenderer.GrassDataList;
            Vector3 center = transform.position;
            
            for (int i = 0; i < count; i++)
            {
                float x = center.x + Random.Range(-spawnAreaSize / 2f, spawnAreaSize / 2f);
                float z = center.z + Random.Range(-spawnAreaSize / 2f, spawnAreaSize / 2f);
                float y = center.y;
                
                if (Physics.Raycast(new Vector3(x, center.y + 50f, z), Vector3.down, out RaycastHit hit, 100f))
                    y = hit.point.y;
                
                float width = Random.Range(0.05f, 0.15f);
                float height = Random.Range(0.2f, 0.6f);
                Color color = new Color(
                    Random.Range(0.2f, 0.4f),
                    Random.Range(0.5f, 0.7f),
                    Random.Range(0.1f, 0.3f)
                );
                
                GrassData data = new GrassData(
                    new Vector3(x, y, z),
                    Vector3.up,
                    width,
                    height,
                    color,
                    0f
                );
                
                grassList.Add(data);
            }
        }
        
        private void OutputResults()
        {
            string separator = "════════════════════════════════════════";
            
            Debug.Log(separator);
            Debug.Log("       GRASS STRESS TEST RESULTS");
            Debug.Log(separator);
            Debug.Log($"  Target FPS:              {targetFPS}");
            Debug.Log($"  Final Grass Count:       {currentGrassCount:N0}");
            Debug.Log($"  Max Sustainable Count:   {maxSustainableCount:N0}");
            Debug.Log($"  Final FPS:               {currentFPS:F1}");
            Debug.Log(separator);
            
            Debug.Log("  RECOMMENDATIONS:");
            if (maxSustainableCount >= 100000)
                Debug.Log("  ✓ Excellent! Can handle 100k+ grass");
            else if (maxSustainableCount >= 50000)
                Debug.Log("  ✓ Good! Switch-compatible (50k+)");
            else if (maxSustainableCount >= 25000)
                Debug.Log("  ⚠ Moderate. Consider reducing density");
            else
                Debug.Log("  ✗ Low. Optimize settings or reduce area");
            
            Debug.Log(separator);
            Debug.Log("\n📋 CLIENT REPORT:");
            Debug.Log($"Grass System Benchmark Results:");
            Debug.Log($"- Maximum grass at {targetFPS} FPS: {maxSustainableCount:N0} instances");
            Debug.Log($"- Test hardware: {SystemInfo.graphicsDeviceName}");
            Debug.Log($"- GPU Memory: {SystemInfo.graphicsMemorySize} MB");
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawWireCube(transform.position, new Vector3(spawnAreaSize, 1, spawnAreaSize));
        }
    }
}
