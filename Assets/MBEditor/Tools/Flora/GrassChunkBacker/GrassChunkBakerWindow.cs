using System.Collections.Generic;
using System.Linq;
using MountAndBlade.Flora;
using UnityEditor;
using UnityEngine;

namespace MountAndBlade.Flora
{
    /// <summary>
    /// Editor window for baking terrain detail prototypes into chunked meshes.
    /// Menu: Mount and Blade → Grass Chunk Baker
    /// </summary>
    public class GrassChunkBakerWindow : EditorWindow
    {
        private Terrain _terrain;
        private GrassChunkSettings _settings;
        private string _outputFolder = "Assets/MBMod/GrassChunks";
        private string _floraKindId = "grass_chunks";

        private Vector2 _scrollPos;
        private bool _showPreview;
        private bool _showChunkDetails;

        // Bake results
        private List<BakedGrassChunk> _bakedChunks;
        private GrassChunksData _lastChunksData;
        private int _totalInstances;
        private int _totalVerts;

        [MenuItem("MBToolset/Tools/Grass Chunk Baker")]
        public static void ShowWindow()
        {
            var window = GetWindow<GrassChunkBakerWindow>("Grass Chunk Baker");
            window.minSize = new Vector2(360, 480);
        }

        private void OnEnable()
        {
            if (_settings == null)
                _settings = new GrassChunkSettings();
            if (_terrain == null)
                _terrain = Terrain.activeTerrain;
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("Grass Chunk Baker", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.HelpBox(
                "Stamps actual detail prototype meshes into spatial chunks.\n" +
                "Preserves original geometry, UVs, normals, and materials.\n" +
                "Each chunk exports as a Warband flora variant.\n\n" +
                "Output includes OBJ meshes, scene prefab, data.json,\n" +
                "and flora_kinds_entry.py - all in one folder.",
                MessageType.Info);

            EditorGUILayout.Space(8);

            _terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", _terrain, typeof(Terrain), true);

            if (_terrain == null)
            {
                EditorGUILayout.HelpBox("Assign a terrain.", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            var td = _terrain.terrainData;
            var protos = td.detailPrototypes;

            // Show prototype summary
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Detail Prototypes", EditorStyles.boldLabel);

            if (protos.Length == 0)
            {
                EditorGUILayout.HelpBox("No detail prototypes on terrain.", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            for (int i = 0; i < protos.Length; i++)
            {
                var p = protos[i];
                string name = p.usePrototypeMesh && p.prototype != null
                    ? p.prototype.name
                    : p.prototypeTexture != null
                        ? p.prototypeTexture.name + " (billboard)"
                        : "(empty)";

                bool isMesh = p.usePrototypeMesh && p.prototype != null;
                string icon = isMesh ? "✔" : "✗";
                Color c = isMesh ? Color.green : Color.gray;
                var prev = GUI.color;
                GUI.color = c;
                EditorGUILayout.LabelField($"  {icon} [{i}] {name}");
                GUI.color = prev;
            }

            int meshProtoCount = protos.Count(p => p.usePrototypeMesh && p.prototype != null);
            if (meshProtoCount == 0)
            {
                EditorGUILayout.HelpBox("No mesh prototypes found. Only mesh-based details can be baked.",
                    MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Chunk Grid", EditorStyles.boldLabel);
            _settings.ChunkSize = EditorGUILayout.Slider("Chunk Size (m)", _settings.ChunkSize, 1f, 1023f);

            int chunksX = Mathf.CeilToInt(td.size.x / _settings.ChunkSize);
            int chunksZ = Mathf.CeilToInt(td.size.z / _settings.ChunkSize);
            EditorGUILayout.LabelField("Grid Size", $"{chunksX} × {chunksZ} = {chunksX * chunksZ} max chunks");

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            _outputFolder = EditorGUILayout.TextField("Asset Folder", _outputFolder);
            _floraKindId = EditorGUILayout.TextField("Flora Kind ID", _floraKindId);

            EditorGUILayout.Space(8);

            if (_bakedChunks != null && _bakedChunks.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Bake Results", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Chunks", _bakedChunks.Count.ToString());
                EditorGUILayout.LabelField("Total Instances", _totalInstances.ToString("N0"));
                EditorGUILayout.LabelField("Total Vertices", _totalVerts.ToString("N0"));
                EditorGUILayout.LabelField("Avg Verts/Chunk",
                    (_totalVerts / _bakedChunks.Count).ToString("N0"));

                int maxVerts = _bakedChunks.Max(c => c.VertexCount);
                if (maxVerts > 65535)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.HelpBox(
                        $"Largest chunk has {maxVerts} vertices - exceeds DX9 16-bit limit (65535).\n" +
                        "Reduce ChunkSize or MaxInstancesPerChunk.",
                        MessageType.Error);
                }

                _showChunkDetails = EditorGUILayout.Foldout(_showChunkDetails, "Per-Chunk Details", true);
                if (_showChunkDetails)
                {
                    EditorGUI.indentLevel++;
                    foreach (var chunk in _bakedChunks.OrderByDescending(c => c.VertexCount).Take(20))
                    {
                        Color vc = chunk.VertexCount > 65535 ? Color.red :
                            chunk.VertexCount > 50000 ? Color.yellow : Color.white;
                        var prev = GUI.color;
                        GUI.color = vc;
                        EditorGUILayout.LabelField(
                            $"[{chunk.ChunkKey.x},{chunk.ChunkKey.y}]",
                            $"{chunk.InstanceCount} inst, {chunk.VertexCount} verts, " +
                            $"{chunk.Materials.Length} mat(s)");
                        GUI.color = prev;
                    }

                    if (_bakedChunks.Count > 20)
                        EditorGUILayout.LabelField($"  ... and {_bakedChunks.Count - 20} more");
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Bake Preview", GUILayout.Height(30)))
            {
                var baker = new GrassChunkBaker(_terrain, _settings);
                _bakedChunks = baker.Bake();
                _totalInstances = _bakedChunks.Sum(c => c.InstanceCount);
                _totalVerts = _bakedChunks.Sum(c => c.VertexCount);
                Repaint();
            }

            GUI.enabled = _bakedChunks != null && _bakedChunks.Count > 0;
            if (GUILayout.Button("Save Assets", GUILayout.Height(30)))
            {
                var chunksData = GrassChunkBakerUtility.BakeAndSave(
                    _terrain, _settings, _outputFolder, _floraKindId);

                if (chunksData != null)
                {
                    _lastChunksData = chunksData;
                    EditorUtility.DisplayDialog("Grass Chunks Saved",
                        $"Saved {chunksData.Chunks.Count} chunks to:\n{_outputFolder}\n\n" +
                        $"Flora Kind: '{_floraKindId}'\n" +
                        $"Variants: {chunksData.FloraData.Meshes.Count}\n" +
                        $"Total: {chunksData.TotalInstances:N0} instances, " +
                        $"{chunksData.TotalVertices:N0} vertices.\n\n" +
                        $"Includes: Meshes/, data.json, flora_kinds_entry.py",
                        "OK");

                    EditorGUIUtility.PingObject(chunksData);
                }
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            GUI.enabled = _bakedChunks != null && _bakedChunks.Count > 0;
            _showPreview = EditorGUILayout.Toggle("Show Scene Gizmos", _showPreview);
            GUI.enabled = true;

            EditorGUILayout.EndScrollView();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_showPreview || _bakedChunks == null) return;

            foreach (var chunk in _bakedChunks)
            {
                Color c = chunk.VertexCount > 65535
                    ? new Color(1f, 0.2f, 0.2f, 0.4f)
                    : new Color(0.2f, 0.8f, 0.2f, 0.3f);
                Handles.color = c;

                var bounds = new Vector3(_settings.ChunkSize, 0.2f, _settings.ChunkSize);
                Handles.DrawWireCube(chunk.Center, bounds);
                Handles.Label(chunk.Center + Vector3.up * 0.5f,
                    $"{chunk.InstanceCount}i / {chunk.VertexCount}v",
                    EditorStyles.miniLabel);
            }
        }

        private void OnFocus()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDestroy()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }
    }
}
