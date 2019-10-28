using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Numerics;
//
using Autodesk.Revit.DB;
//
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Schema2;

namespace RevitExportTest
{

    using VERTEX = VertexPosition;
    using RMaterial = Autodesk.Revit.DB.Material;

    class TestExportContext : IExportContext
    {

        const double _inch_to_mm = 25.4f;
        const double _foot_to_mm = 12 * _inch_to_mm;
        const double _foot_to_m = _foot_to_mm / 1000;

        //Document _doc;
        //Document _сdoc;

        Stack<Document> _documentStack = new Stack<Document>();
        Stack<Transform> _transformationStack = new Stack<Transform>();

        Document CurrentDocument
        {
            get
            {
                return _documentStack.Peek();
            }
        }

        Transform CurrentTransform
        {
            get
            {
                return _transformationStack.Peek();
            }
        }

        public ModelRoot _model;
        Scene _scene;
        Dictionary<string, MaterialBuilder> _materials = new Dictionary<string, MaterialBuilder>();
        MaterialBuilder _material;
        MeshBuilder<VERTEX> _mesh;

        public TestExportContext(Document doc)
        {
            _documentStack.Push(doc);
            _transformationStack.Push(Transform.Identity);
        }

        void SetDefaultMaterial()
        {
            _material = _materials["Default"];
        }

        void SetCurrentMaterial(string uidMaterial)
        {
            if (!_materials.ContainsKey(uidMaterial))
            {
                RMaterial material = CurrentDocument.GetElement(uidMaterial) as RMaterial;

                Color c = material.Color;

                MaterialBuilder m;
                if (material.Transparency != 0)
                {
                    m = new MaterialBuilder()
                        .WithAlpha(AlphaMode.BLEND)
                        .WithDoubleSide(true)
                        .WithMetallicRoughnessShader()
                        .WithChannelParam("BaseColor", new Vector4(c.Red / 256f, c.Green / 256f, c.Blue / 256f, 1 - (material.Transparency / 128f)));
                }
                else
                {
                    m = new MaterialBuilder()
                        .WithDoubleSide(true)
                        .WithMetallicRoughnessShader()
                        .WithChannelParam("BaseColor", new Vector4(c.Red / 256f, c.Green / 256f, c.Blue / 256f, 1));
                }
                m.UseChannel("MetallicRoughness");
                _materials.Add(uidMaterial, m);
            }
            _material = _materials[uidMaterial];
        }

        public void Finish()
        {
            Debug.Print("Finish");
        }

        public bool IsCanceled()
        {
            return false;
        }

        public RenderNodeAction OnElementBegin(ElementId elementId)
        {
            Element e = CurrentDocument.GetElement(elementId);
            if (e != null)
            {
                if (null == e.Category)
                {
                    Debug.WriteLine("\r\n*** Non-category element!\r\n");
                    return RenderNodeAction.Skip;
                }
            }


            _mesh = new MeshBuilder<VERTEX>(elementId.IntegerValue.ToString());
            Debug.Print($"ElementBegin {elementId.IntegerValue}");
            return RenderNodeAction.Proceed;
        }

        public void OnElementEnd(ElementId elementId)
        {
            Element e = CurrentDocument.GetElement(elementId);
            if (e != null)
            {
                if (null == e.Category)
                {
                    Debug.WriteLine("\r\n*** Non-category element!\r\n");
                    return;
                }
            }

            if (_mesh.Primitives.Count > 0) _scene.CreateNode().WithMesh(_model.CreateMeshes(_mesh)[0]);
            Debug.Print($"ElementEnd {elementId.IntegerValue}");
        }

        public RenderNodeAction OnFaceBegin(FaceNode node)
        {
            Debug.Print("OnFaceBegin not implemented.");
            return RenderNodeAction.Skip;
        }

        public void OnFaceEnd(FaceNode node)
        {
            Debug.Print("OnFaceEnd not implemented.");
        }

        public RenderNodeAction OnInstanceBegin(InstanceNode node)
        {
            Debug.Print($"InstanceBegin {node.NodeName}");
            _transformationStack.Push(CurrentTransform.Multiply(node.GetTransform()));
            return RenderNodeAction.Proceed;
        }

        public void OnInstanceEnd(InstanceNode node)
        {
            Debug.Print($"InstanceEnd {node.NodeName}");
            _transformationStack.Pop();
        }

        public void OnLight(LightNode node)
        {
            Debug.Print("OnLight not implemented.");
        }

        public RenderNodeAction OnLinkBegin(LinkNode node)
        {
            Debug.Print($"LinkBegin {node.NodeName}");
            //_сdoc = node.GetDocument();
            _documentStack.Push(node.GetDocument());
            _transformationStack.Push(CurrentTransform.Multiply(node.GetTransform()));
            return RenderNodeAction.Proceed;
        }

        public void OnLinkEnd(LinkNode node)
        {
            Debug.Print($"LinkEnd {node.NodeName}");
            _transformationStack.Pop();
            _documentStack.Pop();
        }

        public void OnMaterial(MaterialNode node)
        {

            ElementId id = node.MaterialId;

            if (ElementId.InvalidElementId != id)
            {
                Element m = CurrentDocument.GetElement(node.MaterialId);
                SetCurrentMaterial(m.UniqueId);
            }
            else SetDefaultMaterial();


            Debug.Print($"Material {node.NodeName}");
        }

        public void OnPolymesh(PolymeshTopology node)
        {

            int nPts = node.NumberOfPoints;
            int nFacets = node.NumberOfFacets;

            Debug.Print($"Polymesh : {nPts} vertices {nFacets} facets");

            IList<XYZ> vertices = node.GetPoints();
            IList<XYZ> normals = node.GetNormals();

            DistributionOfNormals distrib = node.DistributionOfNormals;

            VERTEX[] vertexs = new VERTEX[nPts];
            XYZ p;
            Transform t = CurrentTransform;
            for (int i = 0; i < nPts; i++)
            {
                p = t.OfPoint(node.GetPoint(i));
                //vertexs[i] = new VERTEX((float)(p.Y*_foot_to_m), (float)(p.Z*_foot_to_m), (float)(p.X*_foot_to_m));
                vertexs[i] = new VERTEX((float)(p.Y), (float)(p.Z), (float)(p.X));
            }

            var prim = _mesh.UsePrimitive(_material);

            PolymeshFacet f;
            for (int i = 0; i < nFacets; i++)
            {
                f = node.GetFacet(i);
                prim.AddTriangle(vertexs[f.V1], vertexs[f.V2], vertexs[f.V3]);
            }

        }

        public void OnRPC(RPCNode node)
        {
            Debug.Print($"RPC {node.NodeName}");
        }

        public RenderNodeAction OnViewBegin(ViewNode node)
        {
            Debug.Print($"ViewBegin {node.NodeName}");
            return RenderNodeAction.Proceed;
        }

        public void OnViewEnd(ElementId elementId)
        {
            Debug.Print($"ViewEnd {elementId.IntegerValue}");
        }

        public bool Start()
        {
            Debug.Print("Start");

            _material = new MaterialBuilder()
                .WithDoubleSide(true)
                .WithMetallicRoughnessShader()
                .WithChannelParam("BaseColor", new Vector4(0.5f, 0.5f, 0.5f, 1));
            _material.UseChannel("MetallicRoughness");

            _materials.Add("Default", _material);

            _model = ModelRoot.CreateModel();
            _scene = _model.UseScene("Default");

            return true;
        }
    }
}
