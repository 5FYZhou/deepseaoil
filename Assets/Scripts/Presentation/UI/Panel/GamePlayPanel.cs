using DeepseaOil.Logic;
using TMPro;

namespace DeepseaOil.Presentation.UI
{
    public class GamePlayPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Bottom;
        public override bool CanBeHideByKey => false;

        private TMP_Text txtCountdown;
        private TMP_Text txtNextWave;

        protected override void Awake()
        {
            base.Awake();

            txtCountdown = GetComponent<TMP_Text>("TxtCountdown");

            // 子物体的名字是 TxtNextWave（prefab 里就这么写的）；按 TxtWave 取会拿不到、只留一条 BasePanel 警告。
            txtNextWave = GetComponent<TMP_Text>("TxtNextWave");
        }

        public override void ShowMe() { }

        public override void HideMe() { }

        protected override void OnButtonClicked(string name)
        {
            switch (name)
            {
                case "BtnPause":
                    GameRoot.Instance.Game.ChangeState(GameState.Paused);
                    break;
                case "BtnSetting":
                    //GameRoot.Instance.Game.ChangeState(GameState.Paused);
                    break;
            }
        }
    }
}
