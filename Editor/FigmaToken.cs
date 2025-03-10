using UnityEngine;

namespace FigmaImporter.Editor
{
    [CreateAssetMenu(menuName = "FigmaImporter/Token", fileName = "FigmaToken", order = 0)]
    public class FigmaToken : ScriptableObject
    {
        [SerializeField]
        private string _token;

        public string Token => _token;
    }
}