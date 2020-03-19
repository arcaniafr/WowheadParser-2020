/*
 * * Created by Traesh for AshamaneProject (https://github.com/AshamaneProject)
 */
using Newtonsoft.Json;
using Sql;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using static WowHeadParser.MainWindow;

namespace WowHeadParser.Entities
{
    class SpellTemplateParsing
    {
        public int id;
    }
    
    class SpellLootParsing
    {
        public int id;
        public dynamic modes;
        public int[] stack;
        public int[] creates;

        public string percent;
        public string questRequired;
        public string mode;
    }

    class SpellLootItemParsing : SpellLootParsing
    {
        public int classs;
        public int count;
        public int[] bonustrees;
    }

    class SpellLootCurrencyParsing : SpellLootParsing
    {
        public int category;
        public string icon;
    }
    
    class Spell : Entity
    {
        public Spell()
        {
            m_spellTemplateData = new SpellTemplateParsing();
            m_spellTemplateData.id = 0;
        }

        public Spell(int id)
        {
            m_spellTemplateData = new SpellTemplateParsing();
            m_spellTemplateData.id = id;
        }

        public override String GetWowheadUrl()
        {
            return GetWowheadBaseUrl() + "/spell=" + m_spellTemplateData.id;
        }

        public override List<Entity> GetIdsFromZone(String zoneId, String zoneHtml)
        {
            String pattern = @"new Listview\(\{template: 'npc', id: 'npcs', name: LANG\.tab_npcs, tabs: tabsRelated, parent: 'lkljbjkb574', note: WH\.sprintf\(LANG\.lvnote_filterresults, '\/npcs\?filter=cr=6;crs=" + zoneId + @";crv=0'\), data: (.+)\}\);";
            String spellJSon = Tools.ExtractJsonFromWithPattern(zoneHtml, pattern);

            List<SpellTemplateParsing> parsingArray = JsonConvert.DeserializeObject<List<SpellTemplateParsing>>(spellJSon);
            List<Entity> tempArray = new List<Entity>();
            foreach (SpellTemplateParsing spellTemplateStruct in parsingArray)
            {
                Spell spell = new Spell(spellTemplateStruct.id);
                tempArray.Add(spell);
            }

            return tempArray;
        }

        public override bool ParseSingleJson(int id = 0)
        {
            if (m_spellTemplateData.id == 0 && id == 0)
                return false;
            else if (m_spellTemplateData.id == 0 && id != 0)
                m_spellTemplateData.id = id;

            String spellHtml = Tools.GetHtmlFromWowhead(GetWowheadUrl(), webClient);

            if (spellHtml.Contains("inputbox-error") || spellHtml.Contains("database-detail-page-not-found-message"))
            {
                return false;
            }
            else
            {
                String dataPattern = @"\$\.extend\(g_spells\[" + m_spellTemplateData.id + @"\], (.+)\);";

                String spellTemplateDataJSon = Tools.ExtractJsonFromWithPattern(spellHtml, dataPattern);
                if (spellTemplateDataJSon != null)
                {
                    SpellTemplateParsing spellTemplateData = JsonConvert.DeserializeObject<SpellTemplateParsing>(spellTemplateDataJSon);

                    SetSpellTemplateData(spellTemplateData);

                    // Without m_spellTemplateData we can't really do anything, so return false
                    if (m_spellTemplateData == null)
                        return false;
                }

                if (IsCheckboxChecked("spell loot"))
                {
                    String spellRecipePattern = @"new Listview\({template: 'spell', id: 'recipes', name: LANG\.tab_recipes, tabs: tabsRelated, parent: 'lkljbjkb574', sort: \['skill', 'name'\], hiddenCols: \['slot'\], visibleCols: \['source'\], data:(.+)}\);";
                    String spellRecipeJSon = Tools.ExtractJsonFromWithPattern(spellHtml, spellRecipePattern);
                    if (spellRecipeJSon != null)
                    {
                        SpellLootItemParsing[] spellLootDatas = spellRecipeJSon != null ? JsonConvert.DeserializeObject<SpellLootItemParsing[]>(spellRecipeJSon) : new SpellLootItemParsing[0];
                        SetSpellRecipeData(spellLootDatas);
                    }
                }


                return true;
            }
        }

        public void SetSpellTemplateData(SpellTemplateParsing spellData)
        {
            m_spellTemplateData = spellData;
        }

        public void SetSpellRecipeData(SpellLootItemParsing[] spellLootItemDatas)
        {
            List<SpellLootParsing> lootsData = new List<SpellLootParsing>();

            SpellLootParsing[] allLootData = new SpellLootParsing[spellLootItemDatas.Length];
            Array.Copy(spellLootItemDatas, allLootData, spellLootItemDatas.Length);

            for (uint i = 0; i < allLootData.Length; ++i)
            {
                if (allLootData[i].creates[1] == 0)
                    allLootData[i].creates[1] = 1;
                if (allLootData[i].creates[2] == 0)
                    allLootData[i].creates[2] = 1;
                lootsData.Add(allLootData[i]);
            }
            m_spellLootDatas = lootsData.ToArray();
        }

        public override String GetSQLRequest()
        {
            String returnSql = "";

            if (m_spellTemplateData.id == 0 || isError)
                return returnSql;


            if (IsCheckboxChecked("spell loot") && m_spellLootDatas != null)
            {
                m_spellLootBuilder = new SqlBuilder("spell_loot_template", "entry", SqlQueryType.DeleteInsert);
                m_spellLootBuilder.SetFieldsNames("Item", "Reference", "Chance", "QuestRequired", "LootMode", "GroupId", "MinCount", "MaxCount", "Comment");

                foreach (SpellLootParsing spellLootData in m_spellLootDatas)
                {
                    m_spellLootBuilder.AppendFieldsValue(m_spellTemplateData.id, // Entry
                                                                spellLootData.creates[0], // Item
                                                                0, // Reference
                                                                100, // Chance
                                                                0, // QuestRequired
                                                                1, // LootMode
                                                                0, // GroupId
                                                                spellLootData.creates[1], // MinCount
                                                                spellLootData.creates[2], // MaxCount
                                                                ""); // Comment
                }

                returnSql += m_spellLootBuilder.ToString() + "\n";
            }

            return returnSql;
        }
        
        protected SpellTemplateParsing m_spellTemplateData;
        protected SpellLootParsing[] m_spellLootDatas;

        protected SqlBuilder m_spellLootBuilder;
    }
}
