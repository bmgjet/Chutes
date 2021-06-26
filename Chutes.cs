using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using System;


namespace Oxide.Plugins
{
    [Info("Chutes", "bmgjet", "1.0.0")]
    [Description("Parachute for use with safespace bases")]

    class Chutes : RustPlugin
    {
        #region Declarations
        //Parachute
        public const string permUse = "Chutes.use";
        public const string permAuto = "Chutes.auto";
        public const string permHover = "Chutes.hover";
        public int disableflyhackdelay = 10; //seconds
        public float minheliautochute = 15f;
        public float heliautochutedelay = 1.0f;
        public float chutecooldowns = 1f;
        private static SaveData _data;
        static int parachuteLayer = 1 << (int)Rust.Layer.Water | 1 << (int)Rust.Layer.Transparent | 1 << (int)Rust.Layer.World | 1 << (int)Rust.Layer.Construction | 1 << (int)Rust.Layer.Debris | 1 << (int)Rust.Layer.Default | 1 << (int)Rust.Layer.Terrain | 1 << (int)Rust.Layer.Tree | 1 << (int)Rust.Layer.Vehicle_Large | 1 << (int)Rust.Layer.Deployed;
        #endregion

        #region Hooks
        private void Init()
        {
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permAuto, this);
            permission.RegisterPermission(permHover, this);
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(Name))
                Interface.Oxide.DataFileSystem.GetDatafile(Name).Save();
            Chutes._data = Interface.Oxide.DataFileSystem.ReadObject<SaveData>(Name);
            if(Chutes._data == null)
            {
                WriteSaveData();
            }
        }

        void Unload()
        {
            var objects = GameObject.FindObjectsOfType(typeof(ParaChute));

            if (objects != null)
            {
                foreach (var gameObj in objects)
                {
                    GameObject.Destroy(gameObj);
                }
            }
            if (Chutes._data != null)
            {
                Chutes._data = null;
            }
        }

        void OnNewSave()
        {
            _data.chutecooldown.Clear();
            WriteSaveData();
        }
        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permAuto))
            {
                return;
            }
            player.ChatMessage("AutoChute Attached!");
            FallingCheck(player);
        }
        private object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            if (type == AntiHackType.FlyHack)
            {
                if (Chutes._data.chutecooldown.ContainsKey(player.UserIDString)) //Temp disable flyhack for parachuting.
                {
                    DateTime lastSpawned = Chutes._data.chutecooldown[player.UserIDString];
                    TimeSpan timeRemaining = CeilingTimeSpan(lastSpawned.AddSeconds(disableflyhackdelay) - DateTime.Now);
                    if (timeRemaining.TotalSeconds > 0)
                    {
                        Puts("FLYHACK BLOCKED" + (BasePlayer.Find(player.UserIDString).displayName) + " " + timeRemaining.TotalSeconds.ToString());
                        return true;
                    }
                }
            }
            return null;
        }
        #endregion

        #region Helpers/Functions
        private void FallingCheck(BasePlayer player)
        {
            timer.Once(5, () =>
            {
                if (player.transform.position.y > 800)
                {
                    FallingCheck(player);
                }
                else if (player.transform.position.y > 600)
                {
                    if (player.isMounted) return; //exit loop since already in parachute
                    TryDeployParachuteOnPlayer(player);
                }
            });
        }

        void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permAuto))
            {
                return;
            }
            if (mountable == null || player == null) return;
            if (mountable.ToString().Contains("heli"))
            {
                if (MyHeight(player.transform.position) >= minheliautochute)
                {
                    Timer ChuteCheck = timer.Once(heliautochutedelay, () =>
                    {
                        if (CheckColliders(player, 3f))
                        {
                            return;
                        }
                        player.ChatMessage("<color=red>Deployed Parachute</color>");
                        TryDeployParachuteOnPlayer(player);
                    });
                }
            }
        }

        [ChatCommand("hover")]
        void Hover(BasePlayer player)
        {
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, permHover))
            {
                return;
            }
            var hits = Physics.SphereCastAll(player.transform.position, 10f, Vector3.up);
                var x = new List<ParaChute>();
                foreach (var hit in hits)
                {
                    var entity = hit.GetEntity()?.GetComponent<ParaChute>();
                    if (entity && !x.Contains(entity))
                        if (entity.maxDropSpeed != -14)
                        {
                            entity.maxDropSpeed = -14f;
                            player.ChatMessage("<color=red>Hover Disabled!</color>");
                            return;
                        }
                        else
                        {
                            entity.maxDropSpeed = 0.1f;
                            player.ChatMessage("<color=green>Hover Enabled!</color>");
                            return;
                        }
                }
          }


        [ChatCommand("chute")]
        void chatChute(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, permUse))
            {
                return;
            }
            TryDeployParachuteOnPlayer(player);
        }

        private bool CheckColliders(BasePlayer player, float distance)
        {
            foreach (Collider col in Physics.OverlapSphere(player.transform.position, distance, parachuteLayer))
            {
                string thisobject = col.gameObject.ToString();
                if (thisobject.Contains("modding") || thisobject.Contains("props") || thisobject.Contains("structures") || thisobject.Contains("building core")) { return true; }

                BaseEntity baseEntity = col.gameObject.ToBaseEntity();
                if (baseEntity != null && (baseEntity == player || baseEntity == player.GetComponent<BaseEntity>()))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            return false;
        }

        private bool TryDeployParachuteOnPlayer(BasePlayer player)
        {
            if (player.isMounted)
            {
                player.ChatMessage("<color=red>You can't deploy when mounted.</color>");
                return false;
            }
            if (player.IsOnGround())
            {
                player.ChatMessage("<color=red>You can't deploy a parachute on the ground.</color>");
                return false;
            }

            if (CheckColliders(player, 1.9f))
            {
                player.ChatMessage("<color=orange>Not enough room to deploy parachute!</color>");
                return false;
            }
 
            if (MyHeight(player.transform.position) <= 3.0f)
            {
                player.ChatMessage("Must be higher than 3.0M! You are <color=orange>" + MyHeight(player.transform.position).ToString() + "M</color>");
                return false;
            }

            if (Chutes._data.chutecooldown.ContainsKey(player.UserIDString))
            {
                DateTime lastSpawned = Chutes._data.chutecooldown[player.UserIDString];
                TimeSpan timeRemaining = CeilingTimeSpan(lastSpawned.AddSeconds(chutecooldowns) - DateTime.Now);
                if (timeRemaining.TotalSeconds > 0)
                {
                    player.ChatMessage(string.Format("You have <color=red>{0}</color> until your cooldown ends", timeRemaining.ToString("g")));
                    return false;
                }
                Chutes._data.chutecooldown.Remove(player.UserIDString);
            }

            Chutes._data.chutecooldown.Add(player.UserIDString, DateTime.Now);
            Effect.server.Run("assets/prefabs/deployable/locker/sound/equip_zipper.prefab", player.transform.position);
                    return DeployParachuteOnPositionAndRotation(player, player.transform.position + new Vector3(0f, 7f, 0f), new Vector3(0f, player.GetNetworkRotation().eulerAngles.y, 0f));
        }

        private bool DeployParachuteOnPositionAndRotation(BasePlayer player, Vector3 position, Vector3 rotation)
        {
            try
            {
                BaseEntity worldItem = GameManager.server.CreateEntity("assets/prefabs/misc/burlap sack/generic_world.prefab", position, Quaternion.Euler(rotation), true);
                worldItem.enableSaving = false;
                worldItem.Spawn();
                var sedanRigid = worldItem.gameObject.AddComponent<ParaChute>();
                sedanRigid.SetPlayer(player);
            }
            catch
            {
                return false;
            }
            return true;
        }

        public static float MyHeight(Vector3 Pos)
        {
            float GroundHeight = TerrainMeta.HeightMap.GetHeight(Pos);
            float PlayerHeight = Pos.y;
            float Difference = (PlayerHeight - GroundHeight);
            Difference = (float)Math.Round((Decimal)Difference, 3, MidpointRounding.AwayFromZero);
            return Difference;
        }

        private TimeSpan CeilingTimeSpan(TimeSpan timeSpan) =>
        new TimeSpan((long)Math.Ceiling(1.0 * timeSpan.Ticks / 10000000) * 10000000);

       
        private Vector3 GetIdealFixedPositionForPlayer(BasePlayer player)
        {
            Vector3 forward = player.GetNetworkRotation() * Vector3.forward;
            return player.transform.position + forward.normalized * 6f + Vector3.up * 4f;
        }

        private Quaternion GetIdealRotationForPlayer(BasePlayer player) =>
        Quaternion.Euler(0, player.GetNetworkRotation().eulerAngles.y - 135, 0);
        #endregion

        #region Classes And Overrides
        private void WriteSaveData() =>
        Interface.Oxide.DataFileSystem.WriteObject(Name, Chutes._data);

        class SaveData
        {
            public Dictionary<string, DateTime> chutecooldown = new Dictionary<string, DateTime>();
        }
        #endregion

        public class ParaChute : MonoBehaviour
        {
            Rigidbody myRigidbody;
            BaseEntity worldItem;
            BaseEntity chair;
            BaseEntity parachute;
            public TriggerParent triggerParent;
            BasePlayer player;
            public float upForce = 8f;
            public float maxDropSpeed = -14f;
            public float forwardStrength = 10f;
            public float backwardStrength = 8f;
            public float rotationStrength = 0.4f;
            public float forwardResistance = 0.3f;
            public float rotationResistance = 0.5f;
            public float glidedevider = 3f;
            public float decendmultiplyer = 1.5f;
            public float autoremoveParachuteHeight = 1.0f;
            public float autoremoveParachuteProximity = 1.0f;
            public float angularModifier = 50f;

            public ParaChute()
            {
                worldItem = GetComponent<BaseEntity>();

                parachute = GameManager.server.CreateEntity("assets/prefabs/misc/parachute/parachute.prefab", new Vector3(), new Quaternion(), true);
                parachute.enableSaving = false;
                parachute.transform.localPosition = new Vector3(0f, -7f, 0f);
                parachute?.Spawn();

                string chairprefab = "assets/prefabs/deployable/chair/chair.deployed.prefab";
                chair = GameManager.server.CreateEntity(chairprefab, new Vector3(), new Quaternion(), true);
                chair.skinID = 2495272054;
                chair.enableSaving = false;
                chair.transform.localPosition = new Vector3(0.0f, -1.2f, 0.3f);
                chair.Spawn();
                chair.GetComponent<MeshCollider>().convex = true;
                chair.SetParent(parachute, 0, false, false);
                chair.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                chair.UpdateNetworkGroup();

                parachute.SetParent(worldItem, 0, false, false);

                myRigidbody = worldItem.GetComponent<Rigidbody>();
                myRigidbody.isKinematic = false;
                enabled = false;
            }

            public void SetPlayer(BasePlayer player)
            {
                player.ChatMessage("MOVEMENT - <color=orange>W/A/S/D</color>");
                player.ChatMessage("GLIDE/DECEND/ACCELERATE> - <color=orange>Shift/Ctrl/Reload</color>");
                player.ChatMessage("CUT PARACHUTE - <color=orange>Jump</color>");
                this.player = player;
                chair.GetComponent<BaseMountable>().MountPlayer(player);
                enabled = true;
            }

            public void OnDestroy()
            {
                Release();
                Effect.server.Run("assets/bundled/prefabs/fx/player/groundfall.prefab", player.transform.position);
            }

            public void Release()
            {
                enabled = false;
                if (chair != null && chair.GetComponent<BaseMountable>().IsMounted())
                    chair.GetComponent<BaseMountable>().DismountPlayer(player, false);
                if (player != null && player.isMounted)
                    player.DismountObject();

                if (!chair.IsDestroyed) chair.Kill();
                if (!parachute.IsDestroyed) parachute.Kill();
                if (!worldItem.IsDestroyed) worldItem.Kill();

                UnityEngine.GameObject.Destroy(this.gameObject);
            }

            public void FixedUpdate()
            {
                if (chair == null)
                {
                    OnDestroy();
                    return;
                }
                if (Physics.Raycast(new Ray(chair.transform.position, Vector3.down), autoremoveParachuteHeight, parachuteLayer))
                {
                    OnDestroy();
                    return;
                }

                foreach (Collider col in Physics.OverlapSphere(chair.transform.position, autoremoveParachuteProximity, parachuteLayer))
                {
                    BaseEntity baseEntity = col.gameObject.ToBaseEntity();

                    if (baseEntity != null && (baseEntity == chair || baseEntity == player.GetComponent<BaseEntity>()))
                    {
                        continue;
                    }
                    else
                    {
                        OnDestroy();
                        return;
                    }
                }

                float FlyDistance = Vector3.Distance(player.transform.position, chair.transform.position);
                if (FlyDistance >= 1f) //Distance to travel before resync body.
                {
                    player.Teleport(chair.transform.position);
                }

                if (TerrainMeta.HeightMap.GetHeight(chair.transform.position) >= chair.transform.position.y)
                {
                    Vector3 newPos = chair.transform.position; newPos.y = TerrainMeta.HeightMap.GetHeight(chair.transform.position);
                    OnDestroy();
                    player.Teleport(newPos);
                    return;
                }
                if (player.serverInput.IsDown(BUTTON.JUMP))
                {
                    if (player.transform.position.y > 600f) { player.ChatMessage("<color=red>WARNING</color> - <color=green>Too High To Cut Parachute</color> " + ((int)player.transform.position.y).ToString() + "/600M"); SetPlayer(player); }
                    else
                    {
                        OnDestroy();
                        Effect.server.Run("assets/bundled/prefabs/fx/player/groundfall.prefab", player.transform.position);
                        player.ChatMessage("REDEPLOY - <color=orange>Use Button or Chat /chute</color>");
                        return;
                    }
                }

                if (player.serverInput.IsDown(BUTTON.SPRINT))
                {
                    myRigidbody.AddForce(Vector3.up * ((maxDropSpeed / glidedevider) - myRigidbody.velocity.y), ForceMode.Impulse);
                }

                if (player.serverInput.IsDown(BUTTON.DUCK) && !player.serverInput.IsDown(BUTTON.SPRINT))
                {
                    myRigidbody.AddForce(Vector3.up * ((maxDropSpeed * decendmultiplyer) - myRigidbody.velocity.y), ForceMode.Impulse);
                }

                if (myRigidbody.velocity.y < maxDropSpeed)
                {
                    myRigidbody.AddForce(Vector3.up * (maxDropSpeed - myRigidbody.velocity.y), ForceMode.Impulse);
                }
                myRigidbody.AddForce(Vector3.up * upForce, ForceMode.Acceleration);

                if (myRigidbody.velocity.x < 0f || myRigidbody.velocity.x > 0f || myRigidbody.velocity.z < 0f || myRigidbody.velocity.z > 0f)
                {
                    myRigidbody.AddForce(new Vector3(-myRigidbody.velocity.x, 0f, -myRigidbody.velocity.z) * forwardResistance, ForceMode.Acceleration);
                }
                if (myRigidbody.angularVelocity.y > 0f || myRigidbody.angularVelocity.y > 0f)
                {
                    myRigidbody.AddTorque(new Vector3(0f, -myRigidbody.angularVelocity.y, 0f) * rotationResistance, ForceMode.Acceleration);
                }
                if (player.serverInput.IsDown(BUTTON.RELOAD))
                {
                    myRigidbody.AddForce(myRigidbody.transform.forward * forwardStrength, ForceMode.Acceleration);
                }
                if (player.serverInput.IsDown(BUTTON.FORWARD))
                {
                    myRigidbody.AddForce(myRigidbody.transform.forward * forwardStrength, ForceMode.Acceleration);
                }
                if (player.serverInput.IsDown(BUTTON.BACKWARD))
                {
                    myRigidbody.AddForce(-myRigidbody.transform.forward * backwardStrength, ForceMode.Acceleration);
                }
                if (player.serverInput.IsDown(BUTTON.RIGHT))
                {
                    myRigidbody.AddTorque(Vector3.up * rotationStrength, ForceMode.Acceleration);
                }
                if (player.serverInput.IsDown(BUTTON.LEFT))
                {
                    myRigidbody.AddTorque(Vector3.up * -rotationStrength, ForceMode.Acceleration);
                }
                if (myRigidbody.angularVelocity.y > 0f || myRigidbody.angularVelocity.y < 0f)
                {
                    worldItem.transform.rotation = Quaternion.Euler(worldItem.transform.rotation.eulerAngles.x, worldItem.transform.rotation.eulerAngles.y, -myRigidbody.angularVelocity.y * angularModifier);
                }
                if (Chutes._data.chutecooldown.ContainsKey(player.UserIDString))
                {
                    Chutes._data.chutecooldown[player.UserIDString] = DateTime.Now;
                }
            }
        }
    }
}
