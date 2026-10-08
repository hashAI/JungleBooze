using JungleBooze.Gameplay.Course;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// The 61-second gray-box feel course (spec 101 §6) as data. Tests, bots and the FeelTest scene load it; the
    /// scene builds its geometry from it at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "JungleBooze/Movement/FeelCourse", fileName = "FeelCourse")]
    public sealed class FeelCourseAsset : ScriptableObject
    {
        [SerializeField] private CourseData _course = new CourseData();

        public CourseData Course => _course;

        /// <summary>Editor setup: replaces the layout.</summary>
        public void SetCourse(CourseData course)
        {
            _course = course;
        }
    }
}
