using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    internal sealed class EntryUIPulse : MonoBehaviour
    {
        private Graphic m_Graphic;
        private Color m_BaseColor = Color.white;
        private float m_BaseAlpha;
        private float m_Amplitude;
        private float m_Speed = 1f;
        private bool m_Scale;
        private bool m_Configured;
        private Vector3 m_BaseScale = Vector3.one;

        public void Configure(Color color, float baseAlpha, float amplitude, float speed, bool scale)
        {
            m_Graphic = GetComponent<Graphic>();
            m_BaseColor = color;
            m_BaseAlpha = Mathf.Clamp01(baseAlpha);
            m_Amplitude = Mathf.Max(0f, amplitude);
            m_Speed = Mathf.Max(0.1f, speed);
            m_Scale = scale;
            m_BaseScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
            m_Configured = true;
        }

        private void Update()
        {
            if (!m_Configured)
            {
                return;
            }

            float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * m_Speed) * 0.5f;
            if (m_Graphic == null)
            {
                m_Graphic = GetComponent<Graphic>();
            }

            if (m_Graphic != null)
            {
                var color = m_BaseColor;
                color.a = Mathf.Clamp01(m_BaseAlpha + pulse * m_Amplitude);
                m_Graphic.color = color;
            }

            if (m_Scale)
            {
                transform.localScale = m_BaseScale * (1f + pulse * 0.035f);
            }
        }
    }
}
