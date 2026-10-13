using DeepseaOil.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    public class LevelReader : MonoBehaviour
    {
        private int _curLevelId;
        [SerializeField]
        private int _nextLevelId;

        public CGType cgStart;
        public CGType cgEnd;

        private void Awake()
        {
            // 读当前场景名字，判断是否合法
            // 不合法返回空值 Logic抛出错误
        }
    }
}
