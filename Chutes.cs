using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using System;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Chutes", "bmgjet", "1.0.1")]
    [Description("Parachute for use with safespace bases")]

    class Chutes : RustPlugin
    {
        #region Declarations
        public const string permUse = "Chutes.use";
        public const string permAuto = "Chutes.auto";
        public const string permHover = "Chutes.hover";
        private static SaveData _data;
        private static PluginConfig config;
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
            if (Chutes._data == null)
            {
                WriteSaveData();
            }

            config = Config.ReadObject<PluginConfig>();
            if (config == null)
            {
                LoadDefaultConfig();
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
                Chutes._data = null;

            if (Chutes.config != null)
                Chutes.config = null;
        }

        void OnNewSave()
        {
            _data.chutecooldown.Clear();
            WriteSaveData();
        }
        private object OnEntityTakeDamage(BasePlayer player, HitInfo hitInfo)
        {
            Rust.DamageType damageType = hitInfo.damageTypes.GetMajorityDamageType();
            if (damageType != Rust.DamageType.Suicide) return null;

            var hits = Physics.SphereCastAll(player.transform.position, 10f, Vector3.up);
            var x = new List<ParaChute>();
            foreach (var hit in hits)
            {
                var entity = hit.GetEntity()?.GetComponent<ParaChute>();
                if (entity && !x.Contains(entity))
                {
                    message(player, "Dont");
                    return false; //Prevent Suicide while parachuting
                }
            }
            return null;
        }
        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permAuto))
            {
                return;
            }
            if (MyHeight(player.transform.position) >= config.otherautochute)
            {
                FallingCheck(player);
                message(player, "Auto");
            }

        }
        private object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            if (type == AntiHackType.FlyHack)
            {
                if (Chutes._data.chutecooldown.ContainsKey(player.UserIDString)) //Temp disable flyhack for parachuting.
                {
                    DateTime lastSpawned = Chutes._data.chutecooldown[player.UserIDString];
                    TimeSpan timeRemaining = CeilingTimeSpan(lastSpawned.AddSeconds(config.disableflyhackdelay) - DateTime.Now);
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

        #region Language
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
            {"Deployed", "<color=red>Deployed Parachute</color>"},
            {"HoverD", "<color=red>Hover Disabled!</color>"},
            {"HoverE", "<color=green>Hover Enabled!</color>"},
            {"Mounted", "<color=red>You can't deploy when mounted.</color>"},
            {"Ground", "<color=red>You can't deploy a parachute on the ground.</color>"},
            {"Room", "<color=orange>Not enough room to deploy parachute!</color>"},
            {"HeightFail", "Height: <color=red>{0}</color>"},
            {"Cooldown", "You have <color=red>{0}</color> until your cooldown ends"},
            {"Dont", "Dont Do It Buddy"},
            {"Auto", "AutoChute Attached!"},
            {"Controls", "MOVEMENT - <color=orange>W/A/S/D</color>" + Environment.NewLine + "GLIDE/DECEND/ACCELERATE> - <color=orange>Shift/Ctrl/Reload</color>" + Environment.NewLine + "CUT PARACHUTE - <color=orange>Jump</color>"},
        }, this);
        }

        public void message(BasePlayer chatplayer, string key, params object[] args)
        {
            if (chatplayer == null && !chatplayer.IsConnected) { return; }
            var message = string.Format(lang.GetMessage(key, this, chatplayer.UserIDString), args);
            chatplayer.ChatMessage(message);
        }
        #endregion

        #region Configuration
        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Height off ground to allow parachute:")] public float groundheight { get; set; }
            [JsonProperty(PropertyName = "How long flyhack is disabled after exiting parachute:")] public int disableflyhackdelay { get; set; }
            [JsonProperty(PropertyName = "Hight for auto parachute to acitive after exiting heli:")] public float heliautochute { get; set; }
            [JsonProperty(PropertyName = "Height for auto parachute to active above for sky diving:")] public float otherautochute { get; set; }
            [JsonProperty(PropertyName = "Max Height of Auto Trigger Point:")] public float maxHeightTrigger { get; set; }
            [JsonProperty(PropertyName = "Delay for helicopter ejected parachutes to clear heli:")] public float heliautochutedelay { get; set; }
            [JsonProperty(PropertyName = "Cool down between parachute usage:")] public float chutecooldowns { get; set; }
            [JsonProperty(PropertyName = "Prevent cutting parachute unless bypassed above:")] public float nocutabove { get; set; }
            [JsonProperty(PropertyName = "----Parachute Settings Below----")] public string parachutesettings { get; set; }
            [JsonProperty(PropertyName = "upForce:")] public float upForce { get; set; }
            [JsonProperty(PropertyName = "maxDropSpeed:")] public float maxDropSpeed { get; set; }
            [JsonProperty(PropertyName = "forwardStrength:")] public float forwardStrength { get; set; }
            [JsonProperty(PropertyName = "backwardStrength:")] public float backwardStrength { get; set; }
            [JsonProperty(PropertyName = "rotationStrength:")] public float rotationStrength { get; set; }
            [JsonProperty(PropertyName = "forwardResistance:")] public float forwardResistance { get; set; }
            [JsonProperty(PropertyName = "rotationResistance:")] public float rotationResistance { get; set; }
            [JsonProperty(PropertyName = "glidedevider:")] public float glidedevider { get; set; }
            [JsonProperty(PropertyName = "decendmultiplyer:")] public float decendmultiplyer { get; set; }
            [JsonProperty(PropertyName = "autoremoveParachuteHeight:")] public float autoremoveParachuteHeight { get; set; }
            [JsonProperty(PropertyName = "autoremoveParachuteProximity:")] public float autoremoveParachuteProximity { get; set; }
            [JsonProperty(PropertyName = "angularModifier:")] public float angularModifier { get; set; }
            [JsonProperty(PropertyName = "hoverModifier:")] public float hoverModifier { get; set; }
            [JsonProperty(PropertyName = "Sync Distance:")] public float syncDistance { get; set; }
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                groundheight = 3f,
                disableflyhackdelay = 10,
                heliautochute = 15f,
                otherautochute = 1.25f,
                maxHeightTrigger = 800f,
                heliautochutedelay = 1.0f,
                chutecooldowns = 1,
                nocutabove = 400f,
                upForce = 8f,
                maxDropSpeed = -14f,
                forwardStrength = 10f,
                backwardStrength = 8f,
                rotationStrength = 0.4f,
                forwardResistance = 0.3f,
                rotationResistance = 0.5f,
                glidedevider = 3f,
                decendmultiplyer = 1.5f,
                autoremoveParachuteHeight = 1.0f,
                autoremoveParachuteProximity = 1.5f,
                angularModifier = 50f,
                hoverModifier = 0.1f,
                syncDistance = 2f,
            };
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            Config.WriteObject(GetDefaultConfig(), true);
            config = Config.ReadObject<PluginConfig>();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        }
        #endregion

        #region Helpers/Functions
        public static void ControlInfo(BasePlayer player)
            {

            }
        private void FallingCheck(BasePlayer player)
        {
            timer.Once(4, () =>
            {
                if (player.transform.position.y > 800)
                {
                    FallingCheck(player);
                }
                else if (player.transform.position.y > config.otherautochute)
                {
                    if (player.isMounted) return; //exit loop since already in parachute
                    if (!TryDeployParachuteOnPlayer(player))
                        FallingCheck(player);
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
                if (MyHeight(player.transform.position) >= config.heliautochute)
                {
                    Timer ChuteCheck = timer.Once(config.heliautochutedelay, () =>
                    {
                        if (CheckColliders(player, 3f))
                        {
                            return;
                        }
                        message(player, "Deployed");
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
                    if (entity.maxDropSpeed != config.maxDropSpeed)
                    {
                        entity.maxDropSpeed = config.maxDropSpeed;
                        message(player, "HoverD");
                        return;
                    }
                    else
                    {
                        entity.maxDropSpeed = config.hoverModifier;
                        message(player, "HoverE");
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
                message(player, "Mounted");
                return false;
            }
            if (player.IsOnGround())
            {
                message(player, "Ground");
                return false;
            }

            if (CheckColliders(player, config.autoremoveParachuteProximity))
            {
                message(player, "Room");
                return false;
            }

            if (MyHeight(player.transform.position) <= config.groundheight)
            {
                message(player, "HeightFail", MyHeight(player.transform.position).ToString() + "M / " + config.groundheight.ToString() + "M");
                return false;
            }

            if (Chutes._data.chutecooldown.ContainsKey(player.UserIDString))
            {
                DateTime lastSpawned = Chutes._data.chutecooldown[player.UserIDString];
                TimeSpan timeRemaining = CeilingTimeSpan(lastSpawned.AddSeconds(config.chutecooldowns) - DateTime.Now);
                if (timeRemaining.TotalSeconds > 0)
                {
                    message(player, "Cooldown", timeRemaining.ToString("g"));
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
                message(player, "Controls");
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
            public float maxDropSpeed = config.maxDropSpeed;
            public TriggerParent triggerParent;
            BasePlayer player;

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
                this.player = player;
                chair.GetComponent<BaseMountable>().MountPlayer(player);
                enabled = true;
            }

            public void OnDestroy()
            {
                Release();
                var Landsfx = new Effect("assets/bundled/prefabs/fx/player/groundfall.prefab", player, 0, Vector3.zero, Vector3.forward);
                List<BasePlayer> ClosePlayers = new List<BasePlayer>();
                Vis.Entities<BasePlayer>(player.transform.position, 10f, ClosePlayers); // Get nearby players to play effect to.

                foreach (BasePlayer EffectPlayer in ClosePlayers)
                {
                    if (!EffectPlayer.IsConnected)
                    {
                        continue;
                    }
                    EffectNetwork.Send(Landsfx, EffectPlayer.net.connection);
                }
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
                if (Physics.Raycast(new Ray(chair.transform.position, Vector3.down), config.autoremoveParachuteHeight, parachuteLayer))
                {
                    OnDestroy();
                    return;
                }
                if (!player.IsAlive() || player.IsSleeping())
                {
                    player.EnsureDismounted();
                    OnDestroy();
                    return;
                }

                foreach (Collider col in Physics.OverlapSphere(chair.transform.position, config.autoremoveParachuteProximity, parachuteLayer))
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
                if (FlyDistance >= config.syncDistance && player.isMounted) //Distance to travel before resync body.
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
                    if (player.transform.position.y > config.nocutabove && !player.serverInput.IsDown(BUTTON.RELOAD)) { player.ChatMessage("<color=red>WARNING</color> - <color=green>Too High To Cut Parachute</color> " + ((int)player.transform.position.y).ToString() + "/"+ config.nocutabove.ToString()+"M"); SetPlayer(player); }
                    else
                    {
                        OnDestroy();
                        Effect.server.Run("assets/bundled/prefabs/fx/player/groundfall.prefab", player.transform.position);
                        return;
                    }
                }

                if (player.serverInput.IsDown(BUTTON.SPRINT))
                {
                    myRigidbody.AddForce(Vector3.up * ((maxDropSpeed / config.glidedevider) - myRigidbody.velocity.y), ForceMode.Impulse);
                }

                if (player.serverInput.IsDown(BUTTON.DUCK) && !player.serverInput.IsDown(BUTTON.SPRINT))
                {
                    myRigidbody.AddForce(Vector3.up * ((maxDropSpeed * config.decendmultiplyer) - myRigidbody.velocity.y), ForceMode.Impulse);
                }

                if (myRigidbody.velocity.y < maxDropSpeed)
                {
                    myRigidbody.AddForce(Vector3.up * (maxDropSpeed - myRigidbody.velocity.y), ForceMode.Impulse);
                }
                myRigidbody.AddForce(Vector3.up * config.upForce, ForceMode.Acceleration);

                if (myRigidbody.velocity.x < 0f || myRigidbody.velocity.x > 0f || myRigidbody.velocity.z < 0f || myRigidbody.velocity.z > 0f)
                {
                    myRigidbody.AddForce(new Vector3(-myRigidbody.velocity.x, 0f, -myRigidbody.velocity.z) * config.forwardResistance, ForceMode.Acceleration);
                }
                if (myRigidbody.angularVelocity.y > 0f || myRigidbody.angularVelocity.y > 0f)
                {
                    myRigidbody.AddTorque(new Vector3(0f, -myRigidbody.angularVelocity.y, 0f) * config.rotationResistance, ForceMode.Acceleration);
                }
                if (player.serverInput.IsDown(BUTTON.RELOAD))
                {
                    myRigidbody.AddForce(myRigidbody.transform.forward * config.forwardStrength, ForceMode.Acceleration);
                }
                if (player.serverInput.IsDown(BUTTON.FORWARD))
                {
                    myRigidbody.AddForce(myRigidbody.transform.forward * config.forwardStrength, ForceMode.Acceleration);
                }
                if (player.serverInput.IsDown(BUTTON.BACKWARD))
                {
                    myRigidbody.AddForce(-myRigidbody.transform.forward * config.backwardStrength, ForceMode.Acceleration);
                }
                if (player.serverInput.IsDown(BUTTON.RIGHT))
                {
                    myRigidbody.AddTorque(Vector3.up * config.rotationStrength, ForceMode.Acceleration);
                }
                if (player.serverInput.IsDown(BUTTON.LEFT))
                {
                    myRigidbody.AddTorque(Vector3.up * -config.rotationStrength, ForceMode.Acceleration);
                }
                if (myRigidbody.angularVelocity.y > 0f || myRigidbody.angularVelocity.y < 0f)
                {
                    worldItem.transform.rotation = Quaternion.Euler(worldItem.transform.rotation.eulerAngles.x, worldItem.transform.rotation.eulerAngles.y, -myRigidbody.angularVelocity.y * config.angularModifier);
                }
                if (Chutes._data.chutecooldown.ContainsKey(player.UserIDString))
                {
                    Chutes._data.chutecooldown[player.UserIDString] = DateTime.Now;
                }
            }
        }
    }
}
