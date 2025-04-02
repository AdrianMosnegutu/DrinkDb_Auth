using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DrinkDb_Auth.Model;

namespace DrinkDb_Auth.Adapter
{
    internal interface IResourceAdapter
    {
        public void CreateResource(Resource resource);
        public void UpdateResource(Resource resource);
        public void DeleteResource(Resource resource);
        public Resource GetResourceById(int id);
        public List<Resource> GetResources();
    }
}
