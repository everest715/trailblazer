using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections;

namespace PolygonArsenal
{
    public class PolygonFireProjectile : MonoBehaviour
    {
        RaycastHit hit;
        public GameObject[] projectiles;
        public Transform spawnPosition;
        [HideInInspector]
        public int currentProjectile = 0;
        public float speed = 1000;

        //    MyGUI _GUI;
        PolygonButtonScript selectedProjectileButton;

        void Start()
        {
            selectedProjectileButton = GameObject.Find("Button").GetComponent<PolygonButtonScript>();
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rightArrowKey.wasPressedThisFrame)
                {
                    nextEffect();
                }

                if (keyboard.dKey.wasPressedThisFrame)
                {
                    nextEffect();
                }

                if (keyboard.aKey.wasPressedThisFrame)
                {
                    previousEffect();
                }
                else if (keyboard.leftArrowKey.wasPressedThisFrame)
                {
                    previousEffect();
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 mousePosition = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {

                if (!EventSystem.current.IsPointerOverGameObject())
                {
                    if (Physics.Raycast(Camera.main.ScreenPointToRay(mousePosition), out hit, 100f))
                    {
                        GameObject projectile = Instantiate(projectiles[currentProjectile], spawnPosition.position, Quaternion.identity) as GameObject;
                        projectile.transform.LookAt(hit.point);
                        projectile.GetComponent<Rigidbody>().AddForce(projectile.transform.forward * speed);
                    }
                }

            }
            Debug.DrawRay(Camera.main.ScreenPointToRay(mousePosition).origin, Camera.main.ScreenPointToRay(mousePosition).direction * 100, Color.yellow);
        }

        public void nextEffect()
        {
            if (currentProjectile < projectiles.Length - 1)
                currentProjectile++;
            else
                currentProjectile = 0;
			selectedProjectileButton.getProjectileNames();
        }

        public void previousEffect()
        {
            if (currentProjectile > 0)
                currentProjectile--;
            else
                currentProjectile = projectiles.Length - 1;
			selectedProjectileButton.getProjectileNames();
        }

        public void AdjustSpeed(float newSpeed)
        {
            speed = newSpeed;
        }
    }
}