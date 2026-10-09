using DeepseaOil.Logic;
using TMPro;
using UnityEngine.UI;

namespace DeepseaOil.Presentation.UI
{
    public class DialoguePanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Middle;
        public override bool CanBeHideByKey => false;

        private TMP_Text _txt1;
        private TMP_Text _txt2;
        private Image _imgNPC;
        private Image _imgPlayer;
        private Image _imgBox1;
        private Image _imgBox2;

        public override void ShowMe() { }

        public override void HideMe() { }

        protected override void Awake()
        {
            base.Awake();
            _txt1 = GetComponent<TMP_Text>("Txt1");
            _txt2 = GetComponent<TMP_Text>("Txt2");
            _imgNPC = GetComponent<Image>("ImgNPC");
            _imgPlayer = GetComponent<Image>("ImgPlayer");
            _imgBox1 = GetComponent<Image>("ImgBox1");
            _imgBox2 = GetComponent<Image>("ImgBox2");
        }

    }
}
