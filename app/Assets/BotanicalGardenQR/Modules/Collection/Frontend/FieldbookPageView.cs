using TMPro;
using UnityEngine;

namespace BotanicalGardenQR.Collection.Frontend
{
    /// <summary>The same neutral page prefab displays the supplied record, never a plant-specific fallback.</summary>
    public sealed class FieldbookPageView : MonoBehaviour
    {
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _caption;
        public void Present(string title, string category)
        {
            if (_title != null) _title.text = title ?? string.Empty;
            if (_caption != null) _caption.text = category ?? string.Empty;
        }
    }
}
