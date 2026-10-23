using NUnit.Framework.Interfaces;
using TreeEditor;
using UnityEngine;

namespace NetworkTankArena
{
    public class TankTeamVisuals : MonoBehaviour
    {
        [SerializeField] private Renderer[] teamRenderers;

        [SerializeField] private Material Redcolour;
        [SerializeField] private Material Bluecolour;



        public void SetTeam(TankTeam team)
        {
            Material material = team switch
            {
                TankTeam.Red => Redcolour,
                TankTeam.Blue => Bluecolour,
                _ => null
            };

            if (material == null)
                return;

            foreach (Renderer renderer in teamRenderers)
            {
                if (renderer != null)
                    renderer.sharedMaterial = material;
            }


        }
    }
}
