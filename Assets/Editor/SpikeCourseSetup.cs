using System;
using System.Linq;
using LeftRight.Rebuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LeftRight.Editor
{
    public static class SpikeCourseSetup
    {
        [MenuItem("LeftRight/Rebuild/Update spike pillars")]
        public static void Install()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != PrototypeTools.RebuildScene)
                throw new InvalidOperationException("Open Main outside Play Mode before updating spike pillars.");
            const string path = "Assets/Rebuild/Prefabs/OutpostEnvironment.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                AddTo(prefab);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static void AddTo(GameObject environment)
        {
            var course = environment.GetComponent<SpikeCourse>() ?? environment.AddComponent<SpikeCourse>();
            course.round = environment.GetComponent<RebuildRoundStart>();
            var previous = environment.transform.Find("Rising spike pillars");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var group = new GameObject("Rising spike pillars").transform;
            group.SetParent(environment.transform, false);
            var recruits = environment.GetComponentsInChildren<RecruitMarch>().OrderBy(r => r.role).ToArray();
            course.players = new RecruitController[recruits.Length];
            course.traps = new LaneSpikeTrap[recruits.Length];
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rebuild/Art/Outpost/Models/SpikePillar.fbx");
            const string bloodPath = "Assets/Rebuild/Art/Outpost/Materials/Blood.mat";
            var blood = AssetDatabase.LoadAssetAtPath<Material>(bloodPath);
            if (blood == null)
            {
                blood = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(blood, bloodPath);
            }
            blood.SetColor("_BaseColor", new Color(.30f, .008f, .012f));
            blood.SetFloat("_Smoothness", .65f);
            blood.SetFloat("_Cull", 0);
            EditorUtility.SetDirty(blood);
            if (model == null) throw new InvalidOperationException("Generate SpikePillar.fbx with build_spike_pillar.py first.");
            for (int i = 0; i < recruits.Length; i++)
            {
                var march = recruits[i];
                var player = march.GetComponent<RecruitController>() ?? march.gameObject.AddComponent<RecruitController>();
                player.playerIndex = i;
                // One local client, one controlled recruit. Character/network identity must
                // eventually map into this presentation slot, rather than dictate its lane.
                player.isLocallyControlled = i == 0;
                player.march = march;
                player.round = course.round;
                player.course = course;
                player.ragdoll = march.GetComponent<RecruitRagdoll>() ?? march.gameObject.AddComponent<RecruitRagdoll>();
                var hitAudio = march.GetComponent<RecruitAudio>() ?? march.gameObject.AddComponent<RecruitAudio>();
                var source = march.GetComponent<AudioSource>();
                if (source == null) source = march.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0;
                source.volume = .65f;
                source.priority = 100;
                hitAudio.output = source;
                const string sounds = "Assets/Rebuild/Audio/Recruits/";
                hitAudio.scream = AssetDatabase.LoadAssetAtPath<AudioClip>(sounds + march.role + "Scream.wav");
                hitAudio.spikeImpact = AssetDatabase.LoadAssetAtPath<AudioClip>(sounds + "SpikeImpact.wav");
                hitAudio.squeeze = AssetDatabase.LoadAssetAtPath<AudioClip>(sounds + "Squeeze.wav");
                if (hitAudio.scream == null || hitAudio.spikeImpact == null || hitAudio.squeeze == null)
                    throw new InvalidOperationException("Missing recruit hit sounds.");
                player.hitAudio = hitAudio;
                march.player = player;
                course.players[i] = player;
                var lane = new GameObject("Lane " + (i + 1) + " spike banks").transform;
                lane.SetParent(group, false);
                lane.localPosition = new Vector3(0, .65f, march.transform.localPosition.z);
                var trap = lane.gameObject.AddComponent<LaneSpikeTrap>();
                trap.bloodMaterial = blood;
                course.traps[i] = trap;
                for (int side = 0; side < 2; side++)
                {
                    var bank = new GameObject(side == 0 ? "Left - five pillars" : "Right - five pillars").transform;
                    bank.SetParent(lane, false);
                    bank.localPosition = new Vector3(side == 0 ? -9.72f : 9.72f, 0, 0);
                    bank.localRotation = Quaternion.Euler(0, side == 0 ? 0 : 180, 0);
                    var pillars = new Transform[5];
                    for (int p = 0; p < 5; p++)
                    {
                        var pillar = new GameObject("Rising pillar " + (p + 1)).transform;
                        pillar.SetParent(bank, false);
                        pillar.localPosition = new Vector3(0, 0, (p - 2) * .22f);
                        PrefabUtility.InstantiatePrefab(model, pillar);
                        pillars[p] = pillar;
                    }
                    if (side == 0) { trap.leftBank = bank; trap.leftPillars = pillars; }
                    else { trap.rightBank = bank; trap.rightPillars = pillars; }
                }
            }
            OutpostSceneBuilder.RefreshOutpostMaterialBindings(environment);
            HealthPowerupSetup.AddTo(environment);
        }
    }
}
