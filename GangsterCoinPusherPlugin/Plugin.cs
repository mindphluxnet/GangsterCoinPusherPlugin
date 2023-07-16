using BepInEx;
using LitJson;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace GangsterCoinPusherPlugin
{
    [BepInPlugin("plugin.gcp", "Gangster Coin Pusher Plugin", "1.01")]
    public class Plugin: BaseUnityPlugin
    {
        private bool wasLocaleLoaded = false;
        private bool wasLabelLocaleLoaded = false;
        private Dictionary<SkillItem, int> skillItemState = new Dictionary<SkillItem, int>();
        
        [Serializable]
        public class LocaleData
        {
            public string Id;
            public string Contain;
        }

        [Serializable]
        public class PluginPropertyData
        {
            public int PropId;
            public PropertyType Proptype;
            public int Quantity;
            public int Debris;
            public bool Actived;
            public bool GetAwarded;
        }

        private List<LocaleData> localeData = new List<LocaleData>();
        private List<LanguageTable> labelLocaleData = new List<LanguageTable>();
        private List<PluginPropertyData> playerProperties = new List<PluginPropertyData>();

        void Awake()
        {
            /* Hooks for audio setting fix */
            On.SettingView.AudioClick += SettingView_AudioClick;
            On.SettingView.Start += SettingView_Start;
            On.AudioManager.Awake += AudioManager_Awake;
            On.AudioManager.Init += AudioManager_Init;
            /* Hooks for community localization */
            On.BaseLanguageConfig.GetStringsById += BaseLanguageConfig_GetStringsById;
            On.LanguageLabalConfig.GetDataByKey += LanguageLabalConfig_GetDataByKey;
            /* Hook for Steam achievement fix */
            On.SteamController.SetAchievement += SteamController_SetAchievement;
            /* Hooks for equipped skill fixes */
            On.SkillItem.Refresh += SkillItem_Refresh;
            On.MainMenuView.RefreshEquipSkillSlider += MainMenuView_RefreshEquipSkillSlider;
            On.MainMenuView.SetSkillEnery += MainMenuView_SetSkillEnery;
            On.SkillItem.RunEffect += SkillItem_RunEffect;
            On.SkillItem.ExecuteSkill += SkillItem_ExecuteSkill;
            On.SkillItem.SetEnable += SkillItem_SetEnable;
            /* Fix Lucky Wheel dropping skills that don't exist */
            On.DataMgr.GetRandomSkillType += DataMgr_GetRandomSkillType;            
            /* Hooks for fixing dolls/props collection not saving between sessions */
            On.DataMgr.GetProperty += DataMgr_GetProperty;
            On.DataMgr.SynthesisDebris += DataMgr_SynthesisDebris;
            On.DataMgr.UseProperty += DataMgr_UseProperty;
            On.DataMgr.HasProperty_int += DataMgr_HasProperty_int;
            On.DataMgr.HasProperty_PropertyType += DataMgr_HasProperty_PropertyType;
            On.DataMgr.GetSavePropertyData += DataMgr_GetSavePropertyData;
            On.DataMgr.GetAward += DataMgr_GetAward;
            On.PropItem.GetReward += PropItem_GetReward;
            On.DataMgr.Init += DataMgr_Init;
            /* Hook for community task fix */
            On.CommunityTaskModel.Init += CommunityTaskModel_Init;

            /* Enable the game to run in background if the user alt-tabbed */
            Application.runInBackground = true;
        }
        #region Fix community task 21 to require only 200 coin drops instead of 20k
        private void CommunityTaskModel_Init(On.CommunityTaskModel.orig_Init orig, CommunityTaskModel self)
        {
            CommunityTaskModel.Instance = self;
            self.communityTaskConfig = BaseSingle<ConfigMgr>.Inst.GetConfig<CommunityTaskCoinfigData>();
            self.communityTaskConfig.infos.Find(x => x.ID == 21).Nums = 200;
            self.GetNowCommunityTask(false);
        }
        #endregion

        #region Fix for dolls/props collection not saving between sessions
        private void PropItem_GetReward(On.PropItem.orig_GetReward orig, PropItem self)
        {
            BaseMonoSingle<DataMgr>.Inst.UpdateDiamond(5);
            EffectManager.Instance.GetFlutterEffect(base.transform.position, 3, ItemType.Diamond, 15f);
            BaseMonoSingle<DataMgr>.Inst.GetAward(self.CurData.PropId);
            BaseMonoSingle<Global>.Inst.GetController<PropController>().RefreshRedNode();
            self.gameObject.transform.Find("Award").gameObject.SetActive(IsPropertyActive(self.CurData.PropId) && !IsPropertyAwarded(self.CurData.PropId));            
        }

        private void DataMgr_Init(On.DataMgr.orig_Init orig, DataMgr self)
        {
            orig.Invoke(self);
            LoadPropertySave();
        }

        private bool IsPropertyActive(int propId)
        {
            PluginPropertyData p = playerProperties.Find(x => x.PropId == propId);
            return p.Actived;
        }

        private bool IsPropertyAwarded(int propId)
        {
            PluginPropertyData p = playerProperties.Find(x => x.PropId == propId);
            return p.GetAwarded;
        }


        private void LoadPropertySave()
        {
            SetupPropertyData();
            if (File.Exists(Path.Combine(Application.persistentDataPath, "Properties.json")))
            {
                StringBuilder stringBuilder = new StringBuilder();
                StreamReader streamReader = File.OpenText(Path.Combine(Application.persistentDataPath, "Properties.json"));
                stringBuilder.Append(streamReader.ReadToEnd());
                streamReader.Close();
                streamReader.Dispose();

                try
                {
                    string decrypted = BaseSingle<EncryptDecipherTool>.Inst.Decipher(stringBuilder.ToString());
                    var propData = JsonMapper.ToObject(decrypted);
                    if (propData.Count > 0)
                    {
                        foreach (JsonData item in propData)
                        {
                            PluginPropertyData _propData = new PluginPropertyData();
                            _propData.Actived = (bool)item["Actived"];
                            _propData.Debris = (int)item["Debris"];
                            _propData.GetAwarded = (bool)item["GetAwarded"];
                            _propData.PropId = (int)item["PropId"];
                            _propData.Proptype = (PropertyType)(int)item["Proptype"];
                            _propData.Quantity = (int)item["Quantity"];
                            int i = playerProperties.FindIndex(x => x.PropId == (int)item["PropId"]);
                            if (i != -1)
                            {
                                playerProperties[i] = _propData;
                            }
                        }
                    }
                }
                catch(Exception e)
                {
                    Debug.Log(e.Message);
                }
            }
        }

        private void SetupPropertyData()
        {
            foreach (PropertyData propertyData in BaseSingle<ConfigMgr>.Inst.GetConfig<PropertyConfig>().Properties)
            {
                PluginPropertyData item = new PluginPropertyData
                {
                    PropId = propertyData.ID,
                    Proptype = propertyData.Type,
                    Quantity = 0
                };
                playerProperties.Add(item);
            }
        }

        private void DataMgr_GetAward(On.DataMgr.orig_GetAward orig, DataMgr self, int propId)
        {
            playerProperties.FirstOrDefault((PluginPropertyData x) => x.PropId == propId).GetAwarded = true;
        }

        private PropertySaveData DataMgr_GetSavePropertyData(On.DataMgr.orig_GetSavePropertyData orig, DataMgr self, int propId)
        {
            try
            {                
                PluginPropertyData p = playerProperties.FirstOrDefault((PluginPropertyData x) => x.PropId == propId);
                if (p != null)
                {
                    return new PropertySaveData
                    {
                        Actived = p.Actived,
                        GetAwarded = p.GetAwarded,
                        Debris = p.Debris,
                        PropId = p.PropId,
                        Proptype = p.Proptype,
                        Quantity = p.Quantity
                    };
                }
                else
                {
                    Debug.Log("GetSavePropertyData: NULL");
                    return null;
                }

            }catch(Exception e) { Debug.Log(e.Message); return null; }
            
        }

        private bool DataMgr_HasProperty_PropertyType(On.DataMgr.orig_HasProperty_PropertyType orig, DataMgr self, PropertyType type)
        {
            PluginPropertyData p = playerProperties.FirstOrDefault(x => x.Proptype == type && x.Quantity > 0);
            if(p != null)
            {
                return p.Quantity > 0;
            }
            return false;            
        }

        private bool DataMgr_HasProperty_int(On.DataMgr.orig_HasProperty_int orig, DataMgr self, int propId)
        {
            PluginPropertyData p = playerProperties.FirstOrDefault(x => x.PropId == propId && x.Quantity > 0);
            if(p != null)
            {
                return true;
            }
            return false;            
        }

        private bool DataMgr_UseProperty(On.DataMgr.orig_UseProperty orig, DataMgr self, int propId, int number)
        {
            PluginPropertyData propertySaveData = playerProperties.FirstOrDefault((PluginPropertyData x) => x.PropId == propId);
            if (propertySaveData == null)
            {
                return false;
            }
            if (propertySaveData.Quantity >= number)
            {
                playerProperties.Find(x => x.PropId == propId).Quantity -= number;
                GlobalEvent.DispatchEvent(EGameEvent.PropertyChanged, new object[]
                {
                    propId
                });
                return true;
            }
            return false;
        }

        private void DataMgr_SynthesisDebris(On.DataMgr.orig_SynthesisDebris orig, DataMgr self, int propId)
        {
            PluginPropertyData propertySaveData = playerProperties.FirstOrDefault((PluginPropertyData x) => x.PropId == propId);
            if (propertySaveData.Proptype == PropertyType.Collection && propertySaveData.Debris >= 3)
            {
                playerProperties.Find(x => x.PropId == propId).Quantity++;
                playerProperties.Find(x => x.PropId == propId).Debris -= 3;

                if (!propertySaveData.Actived)
                {
                    playerProperties.Find(x => x.PropId == propId).Actived = true;                    
                }
                Sprite sprite;
                CallBack<int> callBack;
                GlobalSystem.GetDiamond(out sprite, out callBack, 10);
                if (callBack != null)
                {
                    callBack(10);
                }
                UIManager.Instance.ShowTips(Localization.LTabel("100001", Array.Empty<object>()));
                
                GlobalEvent.DispatchEvent(EGameEvent.PropertyChanged, new object[]
                {
                    propId
                });
                GlobalEvent.DispatchEvent("GetProperty", new object[]
                {
                    propId
                });
                
                BaseMonoSingle<Global>.Inst.GetController<PropController>().RefreshRedNode();
                BaseSingle<PaoTaiSystem>.Inst.TestRedNot();
            }
        }

        private void DataMgr_GetProperty(On.DataMgr.orig_GetProperty orig, DataMgr self, int propId, int quantity, bool isall)
        {
            PluginPropertyData propertySaveData = playerProperties.FirstOrDefault((PluginPropertyData x) => x.PropId == propId);
            if (propertySaveData != null)
            {
                if (propertySaveData.Proptype == PropertyType.Collection && !isall)
                {                    
                    playerProperties.Find(x => x.PropId == propId).Debris += quantity;
                }
                else
                {
                    playerProperties.Find(x => x.PropId == propId).Quantity += quantity;                    
                    if (!propertySaveData.Actived)
                    {
                        playerProperties.Find(x => x.PropId == propId).Actived = true;                        
                    }
                }
                AchievementModel.Instance.SendEvent(AchieveType.GetProperty, propId);
                GlobalEvent.DispatchEvent(EGameEvent.PropertyChanged, new object[]
                {
                    propId
                });
                GlobalEvent.DispatchEvent("GetProperty", new object[]
                {
                    propId
                });
                BaseMonoSingle<Global>.Inst.GetController<PropController>().RefreshRedNode();
                BaseSingle<PaoTaiSystem>.Inst.TestRedNot();
            }
        }

        public void SavePropertyData()
        {
            string json = JsonMapper.ToJson(playerProperties);
            string encrypted = BaseSingle<EncryptDecipherTool>.Inst.Encrypt(json);
            byte[] bytes = Encoding.GetEncoding("UTF-8").GetBytes(encrypted);
            File.WriteAllBytes(Path.Combine(Application.persistentDataPath, "Properties.json"), bytes);            
        }

        void OnApplicationQuit()
        {
            SavePropertyData();
        }
        #endregion

        #region Fix for Lucky Wheel drops 
        private SkillType DataMgr_GetRandomSkillType(On.DataMgr.orig_GetRandomSkillType orig, DataMgr self)
        {
            // 5 = Additional Skill (doesn't do anything)
            // 6 = Golden Pig (not really a skill and doesn't do anything either)
            return (SkillType)Random.Range(1, 4);
        }
        #endregion

        #region Fixes for late-game equipped skill energy code breaking the game
        /* Due to an unclamped value equipped skills break completely once they are fully upgraded 
         * These fixes clamp the used value to the maximum instead of relying on MainMenuModel.Instance.SkillSlMax
         * As some values are private we need to work around them so it's a bit more complicated
         */
        private void MainMenuView_SetSkillEnery(On.MainMenuView.orig_SetSkillEnery orig, MainMenuView self, object[] obj)
        {
            if (BaseLocalData<Player>.Data.EquipmentSkillEnerge < GetSkillSlMax())
            {
                BaseLocalData<Player>.Data.EquipmentSkillEnerge++;
                MainMenuView_RefreshEquipSkillSlider(null, self, Array.Empty<object>());
                if (BaseLocalData<Player>.Data.EquipmentSkillId != 0)
                {
                    SkillData skillDataByType = BaseSingle<ConfigMgr>.Inst.GetConfig<SkillConfig>().GetSkillDataByType((SkillType)BaseLocalData<Player>.Data.EquipmentSkillId);
                    self.EquipSkillItem.Type = skillDataByType.SkillType;
                    self.EquipSkillItem.ShowSkillInfo(skillDataByType);
                }
            }
        }

        private void MainMenuView_RefreshEquipSkillSlider(On.MainMenuView.orig_RefreshEquipSkillSlider orig, MainMenuView self, object[] objs)
        {
            self.SlkillEnery.value = (float)BaseLocalData<Player>.Data.EquipmentSkillEnerge / (float)GetSkillSlMax();
            self.itemBase.GetObj("skillready").SetActive(self.SlkillEnery.value == 1f);
        }

        private void SkillItem_Refresh(On.SkillItem.orig_Refresh orig, SkillItem self)
        {
            int skillCount = BaseMonoSingle<DataMgr>.Inst.GetSkillDataByType(self.Type).own;
            self.TxtNumber.text = string.Format("{0}", skillCount);
            self.Lock.gameObject.SetActive(!BaseMonoSingle<DataMgr>.Inst.GetSkillDataByType(self.Type).IsActivation);
            if (!self.IsEquipmentSkill)
            {
                self.SetEnable(skillCount != 0);
            }
            else
            {

                self.SetEnable(BaseLocalData<Player>.Data.EquipmentSkillEnerge >= GetSkillSlMax());
            }
            self.NumBottom.SetActive(!self.IsEquipmentSkill);
        }

        private void SkillItem_SetEnable(On.SkillItem.orig_SetEnable orig, SkillItem self, bool enable)
        {
            enable = (GetSkillItemState(self) == 0 && enable);
            if (self.Type == SkillType.HuLan && EffectsManager.isBumpersRunning)
            {
                enable = false;
            }
            if (EffectsManager.SkillState.ContainsKey(self.Type) && EffectsManager.SkillState[self.Type] == 1)
            {
                enable = false;
            }
            if (self != null)
            {
                self.transform.GetComponent<Image>().color = (enable ? Color.white : Color.gray);
                if (self.transform.parent.name == "EquipmentSkill")
                {
                    MainMenuView.Instance.itemBase.GetObj("skillready").SetActive(enable);
                }
                self.Icon.color = ((!enable) ? Color.grey : Color.white);
            }
        }

        private void SetSkillItemState(SkillItem skillItem, int state)
        {
            if (!skillItemState.ContainsKey(skillItem))
            {
                skillItemState.Add(skillItem, state);
            }
            else
            {
                skillItemState[skillItem] = state;
            }
        }

        private int GetSkillItemState(SkillItem skillItem)
        {
            if (skillItemState.TryGetValue(skillItem, out int state))
            {
                return state;
            }
            return 0;
        }

        private void SkillItem_RunEffect(On.SkillItem.orig_RunEffect orig, SkillItem self, object[] agrs)
        {
            SetSkillItemState(self, (int)agrs[1]);
            if (!self.IsEquipmentSkill)
            {
                self.SetEnable(BaseMonoSingle<DataMgr>.Inst.GetSkillDataByType(self.Type).own != 0);
                return;
            }
            self.SetEnable(BaseLocalData<Player>.Data.EquipmentSkillEnerge >= GetSkillSlMax());
        }

        private void SkillItem_ExecuteSkill(On.SkillItem.orig_ExecuteSkill orig, SkillItem self)
        {
            if (self.IsEquipmentSkill)
            {
                if (BaseLocalData<Player>.Data.EquipmentSkillEnerge >= GetSkillSlMax())
                {
                    BaseLocalData<Player>.Data.EquipmentSkillEnerge = 0;
                    GlobalEvent.DispatchEvent(EGameEvent.ExecuteSkill, new object[]
                    {
                        self.Type,
                        1
                    });
                    GlobalEvent.DispatchEvent("RefreshEquipSkill", Array.Empty<object>());
                    self.Refresh();
                    return;
                }
                UIManager.Instance.ShowTips(Localization.Get("Text_nomoreeng", 0));
                return;
            }
            else
            {
                if (!BaseMonoSingle<DataMgr>.Inst.GetSkillDataByType(self.Type).IsActivation)
                {
                    BaseSingle<CommonTips>.Inst.OpenTips((self.Type == SkillType.BigDou) ? E_Tips.LockDouDong : ((self.Type == SkillType.HuLan) ? E_Tips.LockHuLan : E_Tips.LockTuiTuJi), delegate (bool a)
                    {
                    });
                    return;
                }
                if (BaseMonoSingle<DataMgr>.Inst.GetSkillDataByType(self.Type).own <= 0)
                {
                    UIManager.Instance.ShowTips(Localization.Get("Text_noskilltimes", 0));
                    return;
                }
                if (EffectsManager.SkillState.ContainsKey(self.Type) && EffectsManager.SkillState[self.Type] == 1)
                {
                    return;
                }
                if (self.Type == SkillType.HuLan && EffectsManager.isBumpersRunning)
                {
                    return;
                }
                BaseMonoSingle<DataMgr>.Inst.GetSkillDataByType(self.Type).own--;
                self.Refresh();
                GlobalEvent.DispatchEvent(EGameEvent.ExecuteSkill, new object[]
                {
                    self.Type,
                    1
                });
                BaseMonoSingle<DataMgr>.Inst.RefreshTimeEvent(TimeEvent.UseSkill);
                return;
            }
        }

        private int GetSkillSlMax()
        {
            int lv = BaseMonoSingle<DataMgr>.Inst.GetSkillDataByType(SkillType.AddSkill).Lv;
            float num = BaseSingle<ConfigMgr>.Inst.GetConfig<SkillConfig>().GetSkillDataByType(SkillType.AddSkill).parmes0[Mathf.Clamp(lv, 0, BaseSingle<ConfigMgr>.Inst.GetConfig<SkillConfig>().GetSkillDataByType(SkillType.AddSkill).parmes0.Count - 1)];
            return (int)(ED.GD("skillSlMax") * (100f - num) * 0.01f);
        }
        #endregion

        #region Fix Steam achievements not unlocking right after they have been achieved
        private void SteamController_SetAchievement(On.SteamController.orig_SetAchievement orig, SteamController self, string key)
        {
            orig.Invoke(self, key);
            SteamUserStats.StoreStats();
        }
        #endregion

        #region Use community localization instead of built-in
        private List<string> BaseLanguageConfig_GetStringsById(On.BaseLanguageConfig.orig_GetStringsById orig, BaseLanguageConfig self, string id)
        {
            if(!wasLocaleLoaded)
            {
                wasLocaleLoaded = true;                                
                var locale = JsonMapper.ToObject(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "locale.json")));

                foreach (JsonData item in locale)
                {
                    LocaleData _localeData = new LocaleData();
                    _localeData.Id = item["Id"].ToString();
                    _localeData.Contain = item["Contain"][0].ToString();
                    localeData.Add(_localeData);
                }
            }
            LocaleData foundData = localeData.Find(x => x.Id == id);
            if(foundData != null && foundData.Contain != null && foundData.Contain.Any())
            {
                return new List<string>() { foundData.Contain };
            }
            return new List<string>() { id };            
        }

        private LanguageTable LanguageLabalConfig_GetDataByKey(On.LanguageLabalConfig.orig_GetDataByKey orig, LanguageLabalConfig self, string id)
        {
            if (!wasLabelLocaleLoaded)
            {
                wasLabelLocaleLoaded = true;

                var locale = JsonMapper.ToObject(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "labelLocale.json")));
                foreach (JsonData item in locale)
                {
                    LanguageTable _lt = new LanguageTable();
                    _lt.CN = item["CN"].ToString();
                    _lt.EN = item["EN"].ToString();
                    _lt.ID = item["ID"].ToString();
                    labelLocaleData.Add(_lt);
                }
            }

            LanguageTable foundData = labelLocaleData.Find(x => x.ID == id);
            if (foundData != null)
            {
                return foundData;
            }
            return new LanguageTable() { ID = id, CN = $"missing {id}", EN = $"missing {id}" };
        }

        #endregion

        #region Fix for audio settings not saving
        private void AudioManager_Awake(On.AudioManager.orig_Awake orig, AudioManager self)
        {
            bool shouldMute = PlayerPrefs.GetInt("MuteAudio", 0) == 1;
            self.Mute = shouldMute;
            orig.Invoke(self);
        }
        private void SettingView_Start(On.SettingView.orig_Start orig, SettingView self)
        {
            orig.Invoke(self);
            bool IsOnAudio = PlayerPrefs.GetInt("MuteAudio", 0) == 0 ? true : false;
            self.AudioClick(IsOnAudio);
        }
        private void AudioManager_Init(On.AudioManager.orig_Init orig, AudioManager self, bool IsOnAudio)
        {
            orig.Invoke(self, IsOnAudio);
            IsOnAudio = PlayerPrefs.GetInt("MuteAudio", 0) == 0 ? true : false;
            self.Mute = IsOnAudio;
        }

        private void SettingView_AudioClick(On.SettingView.orig_AudioClick orig, SettingView self, bool value)
        {
            orig.Invoke(self, value);
            PlayerPrefs.SetInt("MuteAudio", AudioManager.Instance.Mute ? 1 : 0);
        }
        #endregion

        #region Turn off audio if alt-tabbed unless it's already muted
        void Update()
        {            
            if (PlayerPrefs.GetInt("MuteAudio") == 1) return;
            AudioManager.Instance.Mute = !Application.isFocused;
        }
        #endregion

    }
}
