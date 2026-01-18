// GrassStressTest.cs - Automated benchmark that finds the grass limit
// Useful for determining max grass count for target hardware

using System.Collections;
using UnityEngine;

namespace GrassSystem
{
    /// <summary>
    /// Automated stress test that progressively adds grass until FPS drops.
    /// Outputs a report showing the maximum sustainable grass count.
    /// </summary>
    public class GrassStressTest : MonoBehaviour
    {
        [Header("Test Settings")]
        [Tooltip("Target FPS to maintain")]
        public int targetFPS = 30;
        
        [Tooltip("How many grass to add per step")]
        public int grassPerStep = 1000;
        
        [Tooltip("Seconds to wait between steps for FPS to stabilize")]
        public float stabilizationTime = 2f;
        
        [Tooltip("Area size to spawn grass (centered on this object)")]
        public float spawnAreaSize = 50f;
        
        [Header("References")]
        public GrassRenderer grassRenderer;
        public MeshFilter targetMesh; // Optional: spawn on this mesh
        
        [Header("Test State")]
        [SerializeField] private bool isRunning;
        [SerializeField] private int currentGrassCount;
        [SerializeField] private float currentFPS;
        [SerializeField] private int maxSustainableCount;
        [SerializeField] private string testStatus = "Idle";
        
        // FPS tracking
        private float[] fpsHistory = new float[30];
        private int fpsIndex;
        private float fpsSum;
        
        private void Start()
        {
            if (grassRenderer == null)
                grassRenderer = FindObjectOfType<GrassRenderer>();
        }
        
        private void Update()
        {
            // Track FPS
            float currentFrameFPS = 1f / Time.unscaledDeltaTime;
            fpsSum -= fpsHistory[fpsIndex];
            fpsHistory[fpsIndex] = currentFrameFPS;
            fpsSum += currentFrameFPS;
            fpsIndex = (fpsIndex + 1) % fpsHistory.Length;
            currentFPS = fpsSum / fpsHistory.Length;
        }
        
        /// <summary>
        /// Start the stress test (call from Inspector button or script)
        /// </summary>
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
        
        /// <summary>
        /// Stop the stress test
        /// </summary>
        [ContextMenu("Stop Test")]
        public void StopTest()
        {
            isRunning = false;
            StopAllCoroutines();
            testStatus = "Stopped";
        }
        
        /// <summary>
        /// Clear all grass and reset
        /// </summary>
        [ContextMenu("Reset")]
        public void ResetTest()
        {
            StopTest();
            if (grassRenderer != null)
            {
                grassRenderer.ClearGrass();
            }
            currentGrassCount = 0;
            maxSustainableCount = 0;
            testStatus = "Reset";
        }
        
        private IEnumerator RunStressTest()
        {
            isRunning = true;
            testStatus = "Initializing...";
            
            // Clear existing grass
            grassRenderer.ClearGrass();
            currentGrassCount = 0;
            maxSustainableCount = 0;
            
            // Wait for FPS to stabilize
            yield return new WaitForSeconds(1f);
            
            testStatus = "Running stress test...";
            Debug.Log("=== GRASS STRESS TEST STARTED ===");
            Debug.Log($"Target FPS: {targetFPS}");
            Debug.Log($"Grass per step: {grassPerStep}");
            
            while (isRunning)
            {
                // Add grass batch
                AddGrassBatch(grassPerStep);
                currentGrassCount = grassRenderer.GrassDataList.Count;
                
                testStatus = $"Testing {currentGrassCount:N0} grass...";
                
                // Rebuild buffers
                grassRenderer.RebuildBuffers();
                
                // Wait for stabilization
                yield return new WaitForSeconds(stabilizationTime);
                
                // Check FPS
                Debug.Log($"Grass: {currentGrassCount:N0} | FPS: {currentFPS:F1}");
                
                if (currentFPS >= targetFPS)
                {
                    // Still good, record this as sustainable
                    maxSustainableCount = currentGrassCount;
                }
                else
                {
                    // FPS dropped below target, we found the limit
                    testStatus = "Test complete!";
                    isRunning = false;
                    break;
                }
                
                // Safety limit
                if (currentGrassCount > 500000)
                {
                    testStatus = "Reached safety limit (500k)";
                    maxSustainableCount = currentGrassCount;
                    isRunning = false;
                    break;
                }
            }
            
            // Output results
            OutputResults();
        }
        
        private void AddGrassBatch(int count)
        {
            var grassList = grassRenderer.GrassDataList;
            Vector3 center = transform.position;
            
            for (int i = 0; i < count; i++)
            {
                // Random position in area
                float x = center.x + Random.Range(-spawnAreaSize / 2f, spawnAreaSize / 2f);
                float z = center.z + Random.Range(-spawnAreaSize / 2f, spawnAreaSize / 2f);
                float y = center.y;
                
                // Raycast to find ground
                if (Physics.Raycast(new Vector3(x, center.y + 50f, z), Vector3.down, out RaycastHit hit, 100f))
                {
                    y = hit.point.y;
                }
                
                // Random variations
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
            
            // Recommendations
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
            
            // Copy-paste summary for client
            Debug.Log("\n📋 CLIENT REPORT (copy this):");
            Debug.Log($"Grass System Benchmark Results:");
            Debug.Log($"- Maximum grass at {targetFPS} FPS: {maxSustainableCount:N0} instances");
            Debug.Log($"- Test hardware: {SystemInfo.graphicsDeviceName}");
            Debug.Log($"- GPU Memory: {SystemInfo.graphicsMemorySize} MB");
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw spawn area
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawWireCube(transform.position, new Vector3(spawnAreaSize, 1, spawnAreaSize));
        }
    }
}
