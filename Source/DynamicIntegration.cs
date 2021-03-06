﻿// MIT License

// Copyright (c) 2017 Mesh Consultants Inc

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

using Rhino.Geometry;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace GrasshopperCs
{    
    public class DynamicIntegration : GH_Component
    {
        static Emitter emitter = null;

        public DynamicIntegration()
            : base("Dynamic Vector Field Integration Simulation", "VFIS", "Dynamically simulates a point travelling through a vector field", "SPM", "Integrate")
        {
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("{BB98017A-3520-4a12-8FD2-D1B2080DBAC9}"); }
        }

        protected override Bitmap Internal_Icon_24x24
        {
            get { return Properties.Resources.Dynamic_Integrator; }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.Register_PointParam("Field Points", "P", "List of points in the vector field (parallel to V)", GH_ParamAccess.list);
            pManager.Register_VectorParam("Field Vectors", "V", "List of vectors in the vector field (parallel to P)", GH_ParamAccess.list);
            pManager.Register_PointParam("Start Points", "SP", "Points to simulate travelling through the vector field", GH_ParamAccess.list);
            pManager.Register_IntegerParam("Particle Life Time", "T", "Number of iterations for each particle to exist, by default these are respawned at their original start points when this time is up", 0);
            pManager.Register_GenericParam("Dynamics", "D", "List of dynamics to modify the vector field with", GH_ParamAccess.list);
            pManager.Register_GenericParam("Settings", "DS", "Dynamic Settings object to customize integration parameters");
            pManager.Register_BooleanParam("Reset simulation", "R", "Reset the simulation to the starting point", true);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.Register_PointParam("Travelled Points", "P", "The Travelling points after they have travelled [step] iterations");
            pManager.Register_VectorParam("Travelled Vectors", "V", "The resultant vectors for each point during the simulation");
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // inputs
            var points = new List<GH_Point>();
            var vectors = new List<GH_Vector>();
            var startPoints = new List<GH_Point>();
            var lifeTime = new GH_Integer();
            var settings = new GH_ObjectWrapper();
            var reset = new GH_Boolean();
            var dynamicsWrapped = new List<GH_ObjectWrapper>();
            var dynamics = new List<IDynamic>();

            if (DA.GetDataList(0, points) && points == null)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid points list. Operation canceled.");
                return;
            }

            if (DA.GetDataList(1, vectors) && vectors == null)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid vector list. Operation canceled.");
                return;
            }

            if (vectors.Count != points.Count && vectors.Count != 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Vector list size mismatch with points list, they must be equal in length (or empty). Operation canceled.");
                return;
            }

            if (DA.GetDataList(2, startPoints) && startPoints == null)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid travelling points. Operation canceled.");
                return;
            }

            if (DA.GetData(3, ref lifeTime) && lifeTime == null)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid lifetime input. Operation canceled.");
                return;
            }

            if (DA.GetDataList(4, dynamicsWrapped) && dynamicsWrapped == null)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid dynamics list. Operation canceled.");
                return;
            }
            dynamics = (from d in dynamicsWrapped select d.Value as IDynamic).ToList();
            Algos.SortDynamicsByPriority(dynamics);

            // if vectors list is empty we'll populate it with empty vectors to match each point
            if (vectors.Count == 0)
                for (int i = 0; i < points.Count; i++)
                    vectors.Add(new GH_Vector());

            // spm parameters component is optional, we use its defaults if it is not available
            var spm_settings = new DynamicSettings();

            if (DA.GetData(5, ref settings))
            {
                // if getdata succeeded but the settings var is null we had bad input
                if (settings == null)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid settings input. Operation canceled.");
                    return;
                }

                // otherwise cast from gh_objectwrapper and continue
                spm_settings = (DynamicSettings)settings.Value;
            }

            if (DA.GetData(6, ref reset) && reset == null)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid reset input. Operation canceled.");
                return;
            }

            if (emitter == null || reset.Value)
            {
                emitter = new Emitter(startPoints, lifeTime.Value, spm_settings);
                DA.SetDataList(0, startPoints);

                var zv = new List<GH_Vector>(startPoints.Count);
                for (int i = 0; i < startPoints.Count; i++)
                    zv.Add(new GH_Vector());

                Algos.ClearDynamics(dynamics);

                DA.SetDataList(1, zv);
                return;
            }

            // emitter updates dynamics
            emitter.Update(dynamics, points, vectors, spm_settings);

            DA.SetDataList(0, emitter.Points);
            DA.SetDataList(1, emitter.Vectors);
        }

    }
}
