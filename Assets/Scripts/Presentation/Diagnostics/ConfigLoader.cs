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
            // 这里只遍历表对象数行数；读列即绕过包装件与"表列迁到 SO"收益，必须先登记
            var tables = ConfigModule.Tables;

            var sb = new System.Text.StringBuilder("[Config] 已加载表：");
            Append(sb, tables.TbProjectile);
            Append(sb, tables.TbEnemy);
            Append(sb, tables.TbTileState);
            Append(sb, tables.TbPlayer);
            Append(sb, tables.TbWave);
            Append(sb, tables.TbTileInitial);
            Append(sb, tables.TbElementRule);
            Append(sb, tables.TbTileEffect);
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
