// 配表工作流 · 运行期消费示例
// 真正的配置入口是 Data 层 ConfigModule，本脚本只做两件事：
//   ① 兜底初始化 —— TestConfig 场景无 GameRoot
//   ② 打到 Console 自检「表 → 生成代码 → JSON」链路
// 时序：GameRoot 在 Awake 里 Init，本脚本 Start 时配置通常已就绪。
// ConfigModule.Init 重复调用会抛异常，必须先问 IsReady。

using DeepseaOil.Data;
using UnityEngine;

namespace DeepseaOil.Presentation.Diagnostics
{
    public class ConfigLoader : MonoBehaviour
    {
        void Start()
        {
            // 兜底：TestConfig 没有 GameRoot
            if (!ConfigModule.IsReady)
                ConfigModule.InitFromStreamingAssets();

            // 逃生舱：直读 cfg.Tables，全库唯一登记破例（见 ConfigModule.Tables）
            // 读表行数；读列即绕过包装件与"表列迁到 SO"收益，必须先登记
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

            var sb = new System.Text.StringBuilder("[Config] 已加载表：");
            Append(sb, tables.TbWeapon);
            Append(sb, tables.TbItem);
            Append(sb, tables.TbFish);
            Debug.Log(sb.ToString());

            // 观测面：拉模型，不推送事件
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
