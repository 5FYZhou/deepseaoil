// ---------------------------------------------------------------------------
// 配表工作流 · 运行期消费示例（可选，删掉不影响导表）
//
// 用法：在 SampleScene 里建个空物体挂上本脚本，进 Play 看 Console。
// 目的只有一个：证明「表 → 生成代码 → StreamingAssets JSON → 运行期对象」这条链路通。
// 真实游戏逻辑请按 Docs/框架草图.md 把配置读取收进 ① Config 配置层。
//
// 注意：本文件落默认程序集 Assembly-CSharp，而生成的配置类（命名空间 cfg）也在
// Assembly-CSharp —— 所以能直接引用，不需要额外 asmdef。
// 将来需要热更时再给生成物划 asmdef，Jam 期不做。
// ---------------------------------------------------------------------------

using System.IO;
using Luban.SimpleJSON;
using UnityEngine;

namespace DeepseaOil.Config
{
    public class ConfigLoader : MonoBehaviour
    {
        /// <summary>Luban 生成的数据目录（相对 StreamingAssets）。</summary>
        const string DataSubDir = "Luban";

        public cfg.Tables Tables { get; private set; }

        void Start()
        {
            string jsonDir = Path.Combine(Application.streamingAssetsPath, DataSubDir);

            // loader 的职责：给它表名，它返回该表的 JSON。
            // 名字 = __tables__.xlsx 的 output 列；留空时默认 <模块>_<表名>（TbWeapon → demo_tbweapon）。
            Tables = new cfg.Tables(file => JSON.Parse(File.ReadAllText(Path.Combine(jsonDir, file + ".json"))));

            var weapon = Tables.TbWeapon.Get(1);
            Debug.Log(string.Format("[Config] 武器 id=1 → {0}  攻击={1}  品质={2}  攻速={3}  图标={4}",
                weapon.Name, weapon.Pow, weapon.Quality, weapon.AtkSpeed, weapon.Icon));

            // 外键链：Fish.best_weapon → Weapon.icon_item → Item
            var fish = Tables.TbFish.Get(1002);
            Debug.Log(string.Format("[Config] 外键：鱼 {0} 推荐武器={1} → {2}",
                fish.Name,
                fish.BestWeapon,
                fish.BestWeapon_Ref != null ? fish.BestWeapon_Ref.Name : "<空>"));
            Debug.Log(string.Format("[Config] 外键：武器 {0} 图标道具={1} → {2}",
                weapon.Name,
                weapon.IconItem,
                weapon.IconItem_Ref != null ? weapon.IconItem_Ref.Name : "<空>"));

            // 各表条数：类型名取自生成类，加表后这里自动跟着变
            var sb = new System.Text.StringBuilder("[Config] 已加载表：");
            Append(sb, Tables.TbWeapon);
            Append(sb, Tables.TbItem);
            Append(sb, Tables.TbFish);
            Debug.Log(sb.ToString());
        }

        static void Append<T>(System.Text.StringBuilder sb, T table)
            where T : class
        {
            var dataList = table.GetType().GetProperty("DataList").GetValue(table) as System.Collections.IList;
            sb.Append("  ").Append(table.GetType().Name)
              .Append(" = ").Append(dataList != null ? dataList.Count : 0);
        }
    }
}
