using UnityEngine;

namespace LeftRight.Rebuild
{
    public sealed class DirectionFloodlights : MonoBehaviour
    {
        public OfficerDirections officer;
        public Light leftLight;
        public Light rightLight;
        public Renderer leftLamp;
        public Renderer rightLamp;
        public int leftLampMaterialIndex;
        public int rightLampMaterialIndex;

        MaterialPropertyBlock leftProperties;
        MaterialPropertyBlock rightProperties;
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        void OnEnable()
        {
            leftProperties = new MaterialPropertyBlock();
            rightProperties = new MaterialPropertyBlock();
            officer.DirectionChanged += SetDirection;
            officer.RunningChanged += SetRunning;
            if (officer.IsRunning && !officer.IsIdling) SetDirection(officer.DirectionRight);
            else SetLights(false, false);
        }

        void OnDisable()
        {
            officer.DirectionChanged -= SetDirection;
            officer.RunningChanged -= SetRunning;
            leftLight.enabled = rightLight.enabled = false;
            leftLamp.SetPropertyBlock(null, leftLampMaterialIndex);
            rightLamp.SetPropertyBlock(null, rightLampMaterialIndex);
        }

        void SetRunning(bool running)
        {
            if (!running || officer.IsIdling) SetLights(false, false);
            else SetDirection(officer.DirectionRight);
        }

        void SetDirection(bool right) => SetLights(!right, right);

        void SetLights(bool left, bool right)
        {
            leftLight.enabled = left;
            rightLight.enabled = right;
            SetLens(leftLamp, leftLampMaterialIndex, leftProperties, left);
            SetLens(rightLamp, rightLampMaterialIndex, rightProperties, right);
        }

        static void SetLens(Renderer lamp, int index, MaterialPropertyBlock properties, bool on)
        {
            lamp.GetPropertyBlock(properties, index);
            properties.SetColor(Emission, on ? new Color(1f, .94f, .80f) * 3.5f : Color.black);
            properties.SetColor(BaseColor, on ? new Color(.95f, .94f, .80f) : new Color(.08f, .09f, .09f));
            lamp.SetPropertyBlock(properties, index);
        }
    }
}
