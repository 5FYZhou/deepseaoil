// ---------------------------------------------------------------------------
// 配表工作流 · 运行期消费示例（可选，删掉不影响导表）
//
// 用法：SampleScene / TestConfig 里挂着本脚本，进 Play 看 Console。
// 目的只有一个：证明「表 → 生成代码 → StreamingAssets JSON → ConfigModule」这条链路通。
//
// 本脚本**不是**配置的唯一入口：真正的入口是 Data 层的 ConfigModule
// （见 Docs/分层设计/数据层.md）。它只做两件事：
//   ① 兜底初始化 —— TestConfig 场景没有 GameRoot，没人 Init
//   ② 把查询结果打到 Console，作为链路自检
//
// 时序：GameRoot 在自己的 Awake 里 Init（Unity 保证所有 Awake 先于任何 Start），
// 所以本脚本的 Start 里 IsReady 通常已经是 true，兜底分支不会走到。
// ConfigModule.Init 重复调用会抛异常，因此必须先问 IsReady —— 不要直接再 Init 一次。
//
// 注意：本文件落默认程序集 Assembly-CSharp，而生成的配置类（命名空间 cfg）也在
// Assembly-CSharp —— 所以能直接引用，不需要额外 asmdef。
// 将来需要热更时再给生成物划 asmdef，Jam 期不做。
// ---------------------------------------------------------------------------

using DeepseaOil.Data;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    public class ConfigLoader : MonoBehaviour
    {
        void Start()
        {
            // 兜底：TestConfig 场景没有 GameRoot，配置还没装配
            if (!ConfigModule.IsReady)
                ConfigModule.InitFromStreamingAssets();

            // 逃生舱：直接读原始 cfg.Tables。只读，且不得跨帧持有该引用。
            var tables = ConfigModule.Tables;

            var weapon = ConfigModule.GetWeapon(1);
            Debug.Log(string.Format("[Config] 武器 id=1 → {0}  攻击={1}  品质={2}  攻速={3}  图标={4}",
                weapon.Name, weapon.Pow, weapon.Quality, weapon.AtkSpeed, weapon.Icon));

            // 外键链：Fish.best_weapon → Weapon.icon_item → Item
            var fish = ConfigModule.GetFish(1002);
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
            Append(sb, tables.TbWeapon);
            Append(sb, tables.TbItem);
            Append(sb, tables.TbFish);
            Debug.Log(sb.ToString());

            // 观测面：拉模型，不推送事件（蓝图 §8 契约表「观测」行）
            var snap = DataMetrics.GetSnapshot();
            Debug.Log(string.Format("[Config] DataMetrics：ConfigReady={0}  TableCount={1}  CachedAssetCount={2}",
                snap.ConfigReady, snap.TableCount, snap.CachedAssetCount));
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
