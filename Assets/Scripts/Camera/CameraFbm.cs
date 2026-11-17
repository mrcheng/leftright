using UnityEngine;

public class CameraFbm : MonoBehaviour
{
    public int octave = 2;
    public float positionFrequency = 0.5f;
    public float rotationFrequency = 0.8f;
    public float positionAmount = 0.15f;
    public float rotationAmount = 1.5f;

    private float positionTime;
    private float rotationTime;
    private Vector3 startPosition;
    private Quaternion startRotation;

    private void Awake()
    {
        startPosition = transform.localPosition;
        startRotation = transform.localRotation;
    }

    private void Update()
    {
        positionTime += Time.deltaTime * positionFrequency;
        rotationTime += Time.deltaTime * rotationFrequency;
        transform.localPosition = startPosition + new Vector3(Perlin.Fbm(positionTime, octave),
            Perlin.Fbm(positionTime + 10f, octave), Perlin.Fbm(positionTime + 20f, octave)) * positionAmount;
        transform.localRotation = startRotation *
            Quaternion.AngleAxis(Perlin.Fbm(rotationTime + 30f, octave) * rotationAmount, Vector3.right) *
            Quaternion.AngleAxis(Perlin.Fbm(rotationTime + 40f, octave) * rotationAmount, Vector3.up);
    }
}
