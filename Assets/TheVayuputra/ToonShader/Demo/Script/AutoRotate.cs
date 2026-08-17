using UnityEngine;

namespace TheVayuputra
{
    public class AutoRotate : MonoBehaviour
    {
        [Header("Rotation Speed (degrees per second)")]
        public Vector3 rotationSpeed = new Vector3(0f, 100f, 0f);

        void Update()
        {
            transform.Rotate(rotationSpeed * Time.deltaTime);
        }
    }
}
