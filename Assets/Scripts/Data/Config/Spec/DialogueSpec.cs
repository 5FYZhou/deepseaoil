using cfg.dso;

namespace DeepseaOil.Data
{
    public sealed class DialogueSpec
    {
        private readonly SlotDialogue _row;

        public DialogueSpec(SlotDialogue row)
        {
            _row = row;
        }

        public int Id => _row.Id;

        public int Paragraph => _row.Paragraph;

        public DialogType Type => _row.Type;

        public AudioId Sfx => _row.Sfx;

        public string Context => _row.Context;
    }
}
