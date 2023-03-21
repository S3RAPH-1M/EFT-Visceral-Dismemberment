﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using Aki.Reflection.Patching;
using Comfort.Common;
using Diz.Skinning;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using Systems.Effects;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.XR;

namespace DismembermentMod
{
    public class DismembermentPatch : ModulePatch
    {
        private static HashSet<String> calibers = new HashSet<String> {
            "Caliber12g",
            "Caliber86x70",
            "Caliber127x55",
            "Caliber20g",
            "Caliber23x75",
            "Caliber762x51",
            "25x59mm",
            "12.7x99",
            "Caliber762x54R",
            "Caliber86x70",
            "86x70"
        };
        public int RandomNumberOutcome
        {
            get;
            set;
        }

        private static Func<Player, InventoryControllerClass> _getInventoryController = player => typeof(Player).GetField("_inventoryController", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(player) as InventoryControllerClass;

        private static Func<Player, BindableState<Item>> _getItemInHands = player => typeof(Player).GetField("_itemInHands", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(player) as BindableState<Item>;

        private static Dictionary<EBodyPart, String> bodyparts = new Dictionary<EBodyPart, String> {
            {
                EBodyPart.Head, "base humanhead"
            },
            {
                EBodyPart.LeftArm,
                "lforearm1"
            },
            {
                EBodyPart.RightArm,
                "rforearm1"
            },
            {
                EBodyPart.LeftLeg,
                "lthigh1"
            },
            {
                EBodyPart.RightLeg,
                "rthigh1"
            }
        };

        protected override MethodBase GetTargetMethod()
        {
            return typeof(Player).GetMethod(nameof(Player.ApplyDamageInfo));
        }

        private static void DismemberLimb(Player player, String bone, String capAssetName, String[] assetNames, out Transform[] affectedLimbs)
        {
            affectedLimbs = EnumerateHierarchyCore(player.Transform.Original).Where(t => t.name.ToLower().Contains(bone) && !ParentContains(t, "weapon_holster")).ToArray();
            foreach (var affectedLimb in affectedLimbs)
            {
                affectedLimb.localScale = Vector3.zero;
                GameObject capAsset = Nexus.BundleLoader.BundleLoaderPlugin.Instance.GetAssetBundle("gorecaps").LoadAsset(capAssetName) as GameObject;
                GameObject instantiatedCapAsset = UnityEngine.Object.Instantiate(capAsset);

                var SkinComponent = instantiatedCapAsset.GetComponentInChildren<Skin>();
                SkinComponent.Init(player.PlayerBody.SkeletonRootJoint);
                SkinComponent.ApplySkin();

                foreach (var assetName in assetNames)
                {
                    GameObject asset = Nexus.BundleLoader.BundleLoaderPlugin.Instance.GetAssetBundle("gorecaps").LoadAsset(assetName) as GameObject;
                    if (asset == null)
                    {
                        ConsoleScreen.LogError($"Dismemberment: DismemberLimb | [{assetName}] not found in gorecaps");
                        continue;
                    }

                    GameObject instantiatedAsset = UnityEngine.Object.Instantiate(asset);
                    instantiatedAsset.transform.position = affectedLimb.position;
                }
            }
        }

        [PatchPostfix]
        private static void Postfix(Player __instance, DamageInfo damageInfo, EBodyPart bodyPartType)
        {
            if (__instance.ActiveHealthController.IsAlive)
            {
                return;
            }

            if (damageInfo.DamageType != EDamageType.Landmine && damageInfo.DamageType != EDamageType.Explosion && damageInfo.DamageType != EDamageType.GrenadeFragment)
            {

                if (!Singleton<ItemFactory>.Instance.ItemTemplates.TryGetValue(damageInfo.SourceId, out ItemTemplate template) || !(template is AmmoTemplate ammoTemplate) || !calibers.Contains(ammoTemplate.Caliber))
                {
                    return;
                }
            }

            Transform[] limbs = null;
            if (damageInfo.DamageType == EDamageType.Landmine || damageInfo.DamageType == EDamageType.Explosion || damageInfo.DamageType == EDamageType.GrenadeFragment)
            {
                if (UnityEngine.Random.Range(0, 2) == 0)
                {
                    DismemberLimb(__instance, "lthigh1", "Leg_LeftCap", new String[] {
                        "gore_leg_torn01"
                    }, out limbs);
                }

                if (UnityEngine.Random.Range(0, 2) == 0)
                {
                    DismemberLimb(__instance, "rthigh1", "Leg_RightCap", new String[] {
                        "gore_leg_torn02"
                    }, out limbs);
                }

                if (UnityEngine.Random.Range(0, 2) == 0)
                {
                    DismemberLimb(__instance, "lforearm1", "Arm_LeftCap", new String[] {
                        "Arm_L_1",
                        "Arm_L_2"
                    }, out limbs);
                }

                if (UnityEngine.Random.Range(0, 2) == 0)
                {
                    DismemberLimb(__instance, "rforearm1", "Arm_RightCap", new String[] {
                        "Arm_R_1",
                        "Arm_R_2"
                    }, out limbs);
                }
            }
            else
            {
                if (!bodyparts.TryGetValue(bodyPartType, out String bone))
                {
                    return;
                }
                switch (bodyPartType)
                {
                    case EBodyPart.LeftArm:
                        {
                            DismemberLimb(__instance, bone, "Arm_LeftCap", new String[] {
                        "Arm_L_1",
                        "Arm_L_2"
                    }, out limbs);
                            break;
                        }
                    case EBodyPart.RightArm:
                        {
                            DismemberLimb(__instance, bone, "Arm_RightCap", new String[] {
                        "Arm_R_1",
                        "Arm_R_2"
                    }, out limbs);
                            break;
                        }
                    case EBodyPart.LeftLeg:
                        {
                            DismemberLimb(__instance, bone, "Leg_LeftCap", new String[] {
                        "gore_leg_torn01"
                    }, out limbs);
                            break;
                        }
                    case EBodyPart.RightLeg:
                        {
                            DismemberLimb(__instance, bone, "Leg_RightCap", new String[] {
                        "gore_leg_torn02"
                    }, out limbs);
                            break;
                        }
                    case EBodyPart.Head:
                        {
                            DismemberLimb(__instance, bone, $"Head_{UnityEngine.Random.Range(1, 4)}", Array.Empty<String>(), out limbs);
                            break;
                        }

                }
            }

            if (limbs == null)
            {
                return;
            }

            SpawnBlood(limbs[0], damageInfo.Direction);
        }

        private static void SpawnBlood(Transform target, Vector3 direction)
        {
            var targetBlodIndex = UnityEngine.Random.Range(1, 17);
            GameObject bloodPrefab = Nexus.BundleLoader.BundleLoaderPlugin.Instance.GetAssetBundle("bloodfx").LoadAllAssets<GameObject>()[targetBlodIndex];
            GameObject brainPrefab = Nexus.BundleLoader.BundleLoaderPlugin.Instance.GetAssetBundle("bloodfx").LoadAllAssets<GameObject>()[18];

            GameObject bloodObject = UnityEngine.Object.Instantiate(bloodPrefab);
            GameObject bainObject = UnityEngine.Object.Instantiate(brainPrefab);
            BFX_BloodSettings bloodSettings = bloodObject.GetComponent<BFX_BloodSettings>();

            bloodObject.transform.position = target.position;
            bloodObject.transform.rotation = Quaternion.Euler(0, -target.TransformDirection(direction).y, 0);
            bloodSettings.GroundHeight = target.position.y - 2;

            bainObject.transform.position = target.position;
            bainObject.transform.rotation.SetEulerAngles(0, target.rotation.y, 0);
        }

        private static IEnumerable<Transform> EnumerateHierarchyCore(Transform root)
        {
            Queue<Transform> transformQueue = new Queue<Transform>();
            transformQueue.Enqueue(root);

            while (transformQueue.Count > 0)
            {
                Transform parentTransform = transformQueue.Dequeue();

                if (!parentTransform)
                {
                    continue;
                }

                for (Int32 i = 0; i < parentTransform.childCount; i++)
                {
                    transformQueue.Enqueue(parentTransform.GetChild(i));
                }

                yield
                return parentTransform;
            }
        }

        private static bool ParentContains(Transform t, String name)
        {
            while (t.parent != null)
            {
                t = t.parent;
                if (t.name.Contains(name))
                {
                    return true;
                }
            }

            return false;
        }
    }
}