﻿using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Collections;
using System.Windows.Forms;
using Newtonsoft.Json;
using Autodesk.Revit.Creation;
using Document = Autodesk.Revit.DB.Document;
using System.Xml.Linq;
using Autodesk.Revit.DB.Architecture;

namespace cityjsonToRevit
{
    [Transaction(TransactionMode.Manual)]
    class DbRunner : IExternalCommand
    {
        static public bool CheckValidity(dynamic file)
        {
            if (file.CityObjects == null || file.type != "CityJSON" || file.version == null ||
                file.transform == null || file.transform.scale == null || file.transform.translate == null ||
                file.vertices == null)
            {
                return false;
            }
            else if (file.version != "1.1" && file.version != "1.0")
            {
                return false;
            }
            return true;
        }
        public void CreateTessellatedShape(Autodesk.Revit.DB.Document doc, ElementId materialId, dynamic cityObjProp, List<XYZ> verticesList)
        {
            List<XYZ> loopVertices = new List<XYZ>();
            TessellatedShapeBuilder builder = new TessellatedShapeBuilder();
            builder.OpenConnectedFaceSet(true);
            foreach (var boundaryGroup in cityObjProp.geometry)
            {
                foreach (var boundary in boundaryGroup.boundaries)
                {
                    loopVertices.Clear();
                    foreach (var facePoints in boundary)
                    {
                        foreach(var facePoint in facePoints)
                        {
                            int VV = unchecked((int)facePoint.Value);
                            XYZ vertPoint = new XYZ(verticesList[VV].X, verticesList[VV].Y, verticesList[VV].Z);
                            loopVertices.Add(vertPoint);
                        }
                        builder.AddFace(new TessellatedFace(loopVertices, materialId));

                    }
                }
                builder.CloseConnectedFaceSet();
                builder.Target = TessellatedShapeBuilderTarget.Solid;
                builder.Fallback = TessellatedShapeBuilderFallback.Abort;
                builder.Build();
                builder.Clear();
                TessellatedShapeBuilderResult result = builder.GetBuildResult();

                DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                ds.ApplicationId = "Application id";
                ds.ApplicationDataId = "Geometry object id";
                //Add Building name to Direct Shape
                ds.Name = "Abbas Bouazar";
                ds.SetShape(result.GetGeometricalObjects());
            }

            
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            //Selecting Default Material for shape creation


            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(Material));

            IEnumerable<Material> materialsEnum
              = collector.ToElements().Cast<Material>().Where(e => e.Name == "Default");
            Material materialDef = materialsEnum.First();


            using (Transaction trans = new Transaction(doc, "Importer"))
            {
                trans.Start();

                var fileContent = string.Empty;
                var filePath = string.Empty;

                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Title = "Open CityJSON file";
                    openFileDialog.InitialDirectory = "c:\\";
                    openFileDialog.Filter = "JSON files (*.JSON)|*.JSON";
                    openFileDialog.FilterIndex = 1;
                    openFileDialog.RestoreDirectory = true;

                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        //Get the path of specified file
                        filePath = openFileDialog.FileName;

                        //Read the contents of the file into a stream
                        var fileStream = openFileDialog.OpenFile();

                        using (StreamReader reader = new StreamReader(fileStream))
                        {
                            string json = reader.ReadToEnd();
                            dynamic jCity = JsonConvert.DeserializeObject(json);
                            List<XYZ> vertList = new List<XYZ>();
                            foreach (var vertex in jCity.vertices)
                            {
                                double x = vertex[0];
                                double y = vertex[1];
                                double z = vertex[2];
                                XYZ vert = new XYZ(x, y, z);
                                vertList.Add(vert);
                            }
                            foreach (var objects in jCity.CityObjects)
                            {
                                foreach(var objProperties in  objects)
                                {

                                        CreateTessellatedShape(doc, materialDef.Id, objProperties, vertList);

                                }
                                
                            }
                                TaskDialog.Show("Good!", "All set! Let's Go!\n");
                        }
                    }
                }



                //CreateTessellatedShape(doc, materialDef.Id);


                //MessageBox.Show(fileContent, "File Content at path: " + filePath, MessageBoxButtons.OK);
                trans.Commit();

                return Result.Succeeded;
            }
        }
    }
}
