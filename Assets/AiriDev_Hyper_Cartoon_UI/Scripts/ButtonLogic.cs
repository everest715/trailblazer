using UnityEngine;

namespace AiriDev.HyperCartoon
{
    public class ButtonLogic : MonoBehaviour
    {
        public GameObject[] images;
        int size = 7;
        
        public void OnNextButtonClick()
        {
            for (int i = 0; i < size; i++)
            {
                if (images[i].activeSelf)
                {
                    images[i].SetActive(false);
                    images[(i + 1) % size].SetActive(true);
                    break;
                }
            }
        }
        public void OnPreviousButtonClick()
        {
            for (int i = 0; i < size; i++)
            {
                if (images[i].activeSelf)
                {
                    images[i].SetActive(false);
                    images[(i - 1 + size) % size].SetActive(true);
                    break;
                }
            }
        }
    }
}
