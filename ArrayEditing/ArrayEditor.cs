using Elements.Core;
using FrooxEngine.UIX;
using FrooxEngine;
using HarmonyLib;
using MonkeyLoader.Patching;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EnumerableToolkit;
using MonkeyLoader.Resonite;
using MonkeyLoader.Resonite.UI.Inspectors;

namespace ArrayEditing
{
    internal sealed class ArrayEditor : ResoniteCancelableEventHandlerMonkey<ArrayEditor, BuildArrayEditorEvent>
    {
        private static readonly MethodInfo _addCurveValueProxying = AccessTools.Method(typeof(ArrayEditor), nameof(AddCurveValueProxying));
        private static readonly MethodInfo _addLinearValueProxying = AccessTools.Method(typeof(ArrayEditor), nameof(AddLinearValueProxying));
        private static readonly MethodInfo _addListReferenceProxying = AccessTools.Method(typeof(ArrayEditor), nameof(AddListReferenceProxying));
        private static readonly MethodInfo _addListValueProxying = AccessTools.Method(typeof(ArrayEditor), nameof(AddListValueProxying));
        private static readonly Type _iWorldElementType = typeof(IWorldElement);
        private static readonly Type _particleBurstType = typeof(ParticleBurst);

        private static readonly MethodInfo _setLinearPoint = AccessTools.Method(typeof(ArrayEditor), nameof(SetLinearPoint));
        private static readonly MethodInfo _setCurvePoint = AccessTools.Method(typeof(ArrayEditor), nameof(SetCurvePoint));

        private static bool _skipListChanges = false;

        public override bool CanBeDisabled => true;

        public override int Priority => HarmonyLib.Priority.High;

        public override bool SkipCanceled => true;

        protected override bool AppliesTo(BuildArrayEditorEvent eventData) => Enabled;

        protected override IEnumerable<IFeaturePatch> GetFeaturePatches() => [];

        protected override void Handle(BuildArrayEditorEvent eventData)
            => eventData.Canceled = BuildArray(eventData.Member, eventData.Name, eventData.FieldInfo, eventData.UI, eventData.LabelSize!.Value);

        private static void AddCurveValueProxying<T>(SyncArray<CurveKey<T>> array, SyncElementList<ValueGradientDriver<T>.Point> list)
            where T : IEquatable<T>
        {
            foreach (var key in array)
            {
                var point = list.Add();
                point.Position.Value = key.time;
                point.Value.Value = key.value;
            }

            AddUpdateProxies(array, list, list.Elements);

            list.ElementsAdded += (list, startIndex, count) =>
            {
                var addedElements = list.Elements.Skip(startIndex).Take(count).ToArray();
                var buffer = addedElements.Select(point => new CurveKey<T>(point.Position, point.Value)).ToArray();

                if (!_skipListChanges)
                {
                    array.Changed -= ArrayChanged;
                    array.Insert(buffer, startIndex);
                    array.Changed += ArrayChanged;
                }
                
                AddUpdateProxies(array, list, addedElements);
            };

            list.ElementsRemoved += (list, startIndex, count) =>
            {
                if (_skipListChanges) return;
                if (array.Count < startIndex + count) return;
                array.Changed -= ArrayChanged;
                array.Remove(startIndex, count);
                array.Changed += ArrayChanged;
            };
        }

        private static void AddLinearValueProxying<T>(SyncArray<LinearKey<T>> array, SyncElementList<ValueGradientDriver<T>.Point> list)
            where T : IEquatable<T>
        {
            foreach (var key in array)
            {
                var point = list.Add();
                point.Position.Value = key.time;
                point.Value.Value = key.value;
            }

            AddUpdateProxies(array, list, list.Elements);

            list.ElementsAdded += (list, startIndex, count) =>
            {
                var addedElements = list.Elements.Skip(startIndex).Take(count).ToArray();
                var buffer = addedElements.Select(point => new LinearKey<T>(point.Position, point.Value)).ToArray();

                if (!_skipListChanges)
                {
                    array.Changed -= ArrayChanged;
                    array.Insert(buffer, startIndex);
                    array.Changed += ArrayChanged;
                }
                AddUpdateProxies(array, list, addedElements);
            };

            list.ElementsRemoved += (list, startIndex, count) =>
            {
                if (_skipListChanges) return;
                if (array.Count < startIndex + count) return;
                array.Changed -= ArrayChanged;
                array.Remove(startIndex, count);
                array.Changed += ArrayChanged;
            };
        }

        private static void AddListReferenceProxying<T>(SyncArray<T> array, SyncElementList<SyncRef<T>> list)
            where T : class, IEquatable<T>, IWorldElement
        {
            foreach (var reference in array)
            {
                var syncRef = list.Add();
                syncRef.Target = reference;
            }

            AddUpdateProxies(array, list, list.Elements);

            list.ElementsAdded += (list, startIndex, count) =>
            {
                var addedElements = list.Elements.Skip(startIndex).Take(count).ToArray();
                var buffer = addedElements.Select(syncRef => syncRef.Target).ToArray();

                if (!_skipListChanges)
                {
                    array.Changed -= ArrayChanged;
                    array.Insert(buffer, startIndex);
                    array.Changed += ArrayChanged;
                }
                AddUpdateProxies(array, list, addedElements);
            };

            list.ElementsRemoved += (list, startIndex, count) =>
            {
                if (_skipListChanges) return;
                if (array.Count < startIndex + count) return;
                array.Changed -= ArrayChanged;
                array.Remove(startIndex, count);
                array.Changed += ArrayChanged;
            };
        }

        private static void AddListValueProxying<T>(SyncArray<T> array, SyncElementList<Sync<T>> list)
            where T : IEquatable<T>
        {
            foreach (var value in array)
            {
                var sync = list.Add();
                sync.Value = value;
            }

            AddUpdateProxies(array, list, list.Elements);

            list.ElementsAdded += (list, startIndex, count) =>
            {
                var addedElements = list.Elements.Skip(startIndex).Take(count).ToArray();
                var buffer = addedElements.Select(sync => sync.Value).ToArray();

                if (!_skipListChanges)
                {
                    array.Changed -= ArrayChanged;
                    array.Insert(buffer, startIndex);
                    array.Changed += ArrayChanged;
                }
                AddUpdateProxies(array, list, addedElements);
            };

            list.ElementsRemoved += (list, startIndex, count) =>
            {
                if (_skipListChanges) return;
                if (array.Count < startIndex + count) return;
                array.Changed -= ArrayChanged;
                array.Remove(startIndex, count);
                array.Changed += ArrayChanged;
            };
        }

        private static void AddParticleBurstListProxying(SyncArray<LinearKey<ParticleBurst>> array, SyncElementList<ValueGradientDriver<int2>.Point> list)
        {
            foreach (var burst in array)
            {
                var point = list.Add();
                point.Position.Value = burst.time;
                point.Value.Value = new int2(burst.value.minCount, burst.value.maxCount);
            }

            AddUpdateProxies(array, list, list.Elements);

            list.ElementsAdded += (list, startIndex, count) =>
            {
                var addedElements = list.Elements.Skip(startIndex).Take(count).ToArray();
                var buffer = addedElements.Select(point => new LinearKey<ParticleBurst>(point.Position, new ParticleBurst() { minCount = point.Value.Value.x, maxCount = point.Value.Value.y })).ToArray();

                if (!_skipListChanges)
                {
                    array.Changed -= ArrayChanged;
                    array.Insert(buffer, startIndex);
                    array.Changed += ArrayChanged;
                }
                AddUpdateProxies(array, list, addedElements);
            };

            list.ElementsRemoved += (list, startIndex, count) => 
            {
                if (_skipListChanges) return;
                if (array.Count < startIndex + count) return;
                array.Changed -= ArrayChanged;
                array.Remove(startIndex, count);
                array.Changed += ArrayChanged;
            };
        }

        private static void AddTubePointProxying(SyncArray<TubePoint> array, SyncElementList<ValueGradientDriver<float3>.Point> list)
        {
            foreach (var tubePoint in array)
            {
                var point = list.Add();
                point.Position.Value = tubePoint.radius;
                point.Value.Value = tubePoint.position;
            }

            AddUpdateProxies(array, list, list.Elements);

            list.ElementsAdded += (list, startIndex, count) =>
            {
                var addedElements = list.Elements.Skip(startIndex).Take(count).ToArray();
                var buffer = addedElements.Select(point => new TubePoint(point.Value.Value, point.Position.Value)).ToArray();

                if (!_skipListChanges)
                {
                    array.Changed -= ArrayChanged;
                    array.Insert(buffer, startIndex);
                    array.Changed += ArrayChanged;
                }
                AddUpdateProxies(array, list, addedElements);
            };

            list.ElementsRemoved += (list, startIndex, count) =>
            {
                if (_skipListChanges) return;
                if (array.Count < startIndex + count) return;
                array.Changed -= ArrayChanged;
                array.Remove(startIndex, count);
                array.Changed += ArrayChanged;
            };
        }

        private static void AddUpdateProxies<T>(SyncArray<LinearKey<T>> array,
            SyncElementList<ValueGradientDriver<T>.Point> list, IEnumerable<ValueGradientDriver<T>.Point> elements)
                    where T : IEquatable<T>
        {
            foreach (var point in elements)
            {
                point.Changed += syncObject =>
                {
                    if (_skipListChanges) return;
                    var index = list.IndexOfElement(point);
                    array.Changed -= ArrayChanged;
                    array[index] = new LinearKey<T>(point.Position, point.Value);
                    array.Changed += ArrayChanged;
                };
            }
        }

        private static void AddUpdateProxies(SyncArray<LinearKey<ParticleBurst>> array,
            SyncElementList<ValueGradientDriver<int2>.Point> list, IEnumerable<ValueGradientDriver<int2>.Point> elements)
        {
            foreach (var point in elements)
            {
                point.Changed += field =>
                {
                    if (_skipListChanges) return;
                    var index = list.IndexOfElement(point);
                    var key = new LinearKey<ParticleBurst>(point.Position, new ParticleBurst() { minCount = point.Value.Value.x, maxCount = point.Value.Value.y });
                    array.Changed -= ArrayChanged;
                    array[index] = key;
                    array.Changed += ArrayChanged;
                };
            }
        }

        private static void AddUpdateProxies<T>(SyncArray<T> array, SyncElementList<Sync<T>> list, IEnumerable<Sync<T>> elements)
                    where T : IEquatable<T>
        {
            foreach (var sync in elements)
            {
                sync.OnValueChange += field =>
                {
                    if (_skipListChanges) return;
                    var index = list.IndexOfElement(sync);
                    array.Changed -= ArrayChanged;
                    array[index] = sync.Value;
                    array.Changed += ArrayChanged;
                };
            }
        }

        private static void AddUpdateProxies<T>(SyncArray<T> array, SyncElementList<SyncRef<T>> list, IEnumerable<SyncRef<T>> elements)
            where T : class, IEquatable<T>, IWorldElement
        {
            foreach (var sync in elements)
            {
                sync.OnValueChange += field =>
                {
                    if (_skipListChanges) return;
                    var index = list.IndexOfElement(sync);
                    array.Changed -= ArrayChanged;
                    array[index] = sync.Target;
                    array.Changed += ArrayChanged;
                };
            }
        }

        private static void AddUpdateProxies(SyncArray<TubePoint> array, SyncElementList<ValueGradientDriver<float3>.Point> list, IEnumerable<ValueGradientDriver<float3>.Point> elements)
        {
            foreach (var point in elements)
            {
                point.Changed += field =>
                {
                    if (_skipListChanges) return;
                    var index = list.IndexOfElement(point);
                    var tubePoint = new TubePoint(point.Value.Value, point.Position.Value);
                    array.Changed -= ArrayChanged;
                    array[index] = tubePoint;
                    array.Changed += ArrayChanged;
                };
            }
        }

        private static void AddUpdateProxies<T>(SyncArray<CurveKey<T>> array,
            SyncElementList<ValueGradientDriver<T>.Point> list, IEnumerable<ValueGradientDriver<T>.Point> elements)
                    where T : IEquatable<T>
        {
            foreach (var point in elements)
            {
                point.Changed += syncObject =>
                {
                    if (_skipListChanges) return;
                    var index = list.IndexOfElement(point);
                    array.Changed -= ArrayChanged;
                    array[index] = new CurveKey<T>(point.Position, point.Value, array[index].leftTangent, array[index].rightTangent);
                    array.Changed += ArrayChanged;
                };
            }
        }

        private static bool BuildArray(ISyncArray array, string name, FieldInfo fieldInfo, UIBuilder ui, float labelSize)
        {
            if (!TryGetGenericParameters(typeof(SyncArray<>), array.GetType(), out var genericParameters))
                return false;

            var isSyncLinear = TryGetGenericParameters(typeof(SyncLinear<>), array.GetType(), out var syncLinearGenericParameters);

            var isSyncCurve = TryGetGenericParameters(typeof(SyncCurve<>), array.GetType(), out var syncCurveGenericParameters);

            var arrayType = genericParameters!.Value.First();
            var syncLinearType = syncLinearGenericParameters?.First();
            var syncCurveType = syncCurveGenericParameters?.First();

            var isParticleBurst = syncLinearType == _particleBurstType;

            if (isSyncLinear && isParticleBurst)
                syncLinearType = typeof(int2);

            var proxySlotName = $"{name}-{array.ReferenceID}-Proxy";
            var proxiesSlot = ui.World.AssetsSlot;
            var newProxy = false;
            if (proxiesSlot.FindChild(proxySlotName) is not Slot proxySlot)
            {
                proxySlot = proxiesSlot.AddSlot(proxySlotName);
                array.FindNearestParent<IDestroyable>().Destroyed += (IDestroyable _) => proxySlot.Destroy();
                newProxy = true;
            }
            proxySlot.DestroyWhenLocalUserLeaves();

            ISyncList list;
            FieldInfo listField;

            if (isSyncLinear && SupportsLerp(syncLinearType!))
            {
                var gradientType = typeof(ValueGradientDriver<>).MakeGenericType(syncLinearType);
                var gradient = GetOrAttachComponent(proxySlot, gradientType, out var attachedNew);

                list = (ISyncList)gradient.GetSyncMember(nameof(ValueGradientDriver<float>.Points));
                listField = gradient.GetSyncMemberFieldInfo(nameof(ValueGradientDriver<float>.Points));

                if (attachedNew)
                {
                    if (isParticleBurst)
                        AddParticleBurstListProxying((SyncArray<LinearKey<ParticleBurst>>)array, (SyncElementList<ValueGradientDriver<int2>.Point>)list);
                    else
                        _addLinearValueProxying.MakeGenericMethod(syncLinearType).Invoke(null, [array, list]);
                }
            }
            else if (isSyncCurve && SupportsLerp(syncCurveType!))
            {
                var gradientType = typeof(ValueGradientDriver<>).MakeGenericType(syncCurveType);
                var gradient = GetOrAttachComponent(proxySlot, gradientType, out var attachedNew);

                list = (ISyncList)gradient.GetSyncMember(nameof(ValueGradientDriver<float>.Points));
                listField = gradient.GetSyncMemberFieldInfo(nameof(ValueGradientDriver<float>.Points));

                if (attachedNew)
                {
                    _addCurveValueProxying.MakeGenericMethod(syncCurveType).Invoke(null, [array, list]);
                }
            }
            else
            {
                if (arrayType == typeof(TubePoint))
                {
                    var gradient = GetOrAttachComponent(proxySlot, typeof(ValueGradientDriver<float3>), out var attachedNew);

                    list = (ISyncList)gradient.GetSyncMember(nameof(ValueGradientDriver<float3>.Points));
                    listField = gradient.GetSyncMemberFieldInfo(nameof(ValueGradientDriver<float3>.Points));

                    if (attachedNew)
                    {
                        AddTubePointProxying((SyncArray<TubePoint>)array, (SyncElementList<ValueGradientDriver<float3>.Point>)list);
                    }
                }
                else if (Coder.IsEnginePrimitive(arrayType))
                {
                    var multiplexerType = typeof(ValueMultiplexer<>).MakeGenericType(arrayType);
                    var multiplexer = GetOrAttachComponent(proxySlot, multiplexerType, out var attachedNew);
                    list = (ISyncList)multiplexer.GetSyncMember(nameof(ValueMultiplexer<float>.Values));
                    listField = multiplexer.GetSyncMemberFieldInfo(nameof(ValueMultiplexer<float>.Values));

                    if (attachedNew)
                        _addListValueProxying.MakeGenericMethod(arrayType).Invoke(null, [array, list]);
                }
                else if (_iWorldElementType.IsAssignableFrom(arrayType))
                {
                    var multiplexerType = typeof(ReferenceMultiplexer<>).MakeGenericType(arrayType);
                    var multiplexer = GetOrAttachComponent(proxySlot, multiplexerType, out var attachedNew);
                    list = (ISyncList)multiplexer.GetSyncMember(nameof(ReferenceMultiplexer<Slot>.References));
                    listField = multiplexer.GetSyncMemberFieldInfo(nameof(ReferenceMultiplexer<Slot>.References));

                    if (attachedNew)
                        _addListReferenceProxying.MakeGenericMethod(arrayType).Invoke(null, [array, list]);
                }
                else
                {
                    proxySlot.Destroy();
                    return false;
                }
            }

            ui.Panel();//.Slot.GetComponent<LayoutElement>();
            var memberFieldSlot = SyncMemberEditorBuilder.GenerateMemberField(array, name, ui, labelSize);
            ui.NestOut();

            if (!array.IsDriven)
            {
                SyncMemberEditorBuilder.BuildList(list, name, listField, ui);
                var listSlot = ui.Current;
                listSlot.DestroyWhenLocalUserLeaves();
                void ArrayDriveCheck(IChangeable changeable)
                {
                    if (((ISyncArray)changeable).IsDriven)
                    {
                        listSlot.DestroyChildren();
                        listSlot.Components.ToArray().Do((Component c) => c.Destroy());
                        listSlot.AttachComponent<LayoutElement>().MinHeight.Value = 24f;
                        var newUi = new UIBuilder(listSlot, listSlot);
                        RadiantUI_Constants.SetupEditorStyle(newUi);
                        newUi.Text("(array is driven)");
                        proxySlot?.Destroy();
                        array.Changed -= ArrayDriveCheck;
                    }
                }
                array.Changed += ArrayDriveCheck;
            }
            else
            {
                LocaleString text = "(array is driven)";
                ui.Text(in text);
            }

            if (newProxy)
            {
                array.Changed += ArrayChanged;
            }

            return true;
        }

        // doesn't work?
        static void SetParticlePoint(ValueGradientDriver<int2>.Point point, LinearKey<ParticleBurst> arrayElem)
        {
            point.Position.Value = arrayElem.time;
            point.Value.Value = new int2(arrayElem.value.minCount, arrayElem.value.maxCount);
        }

        static void SetLinearPoint<T>(ValueGradientDriver<T>.Point point, LinearKey<T> arrayElem) where T : IEquatable<T>
        {
            point.Position.Value = arrayElem.time;
            point.Value.Value = arrayElem.value;
        }

        static void SetCurvePoint<T>(ValueGradientDriver<T>.Point point, CurveKey<T> arrayElem) where T : IEquatable<T>
        {
            point.Position.Value = arrayElem.time;
            point.Value.Value = arrayElem.value;
        }

        static void SetTubePoint(ValueGradientDriver<float3>.Point point, TubePoint arrayElem)
        {
            point.Position.Value = arrayElem.radius;
            point.Value.Value = arrayElem.position;
        }

        static void ArrayChanged(IChangeable changeable)
        {
            var array = (ISyncArray)changeable;

            if (array.IsDriven)
            {
                array.Changed -= ArrayChanged;
                return;
            }

            var proxySlotName = $"{array.Name}-{array.ReferenceID}-Proxy";
            var proxiesSlot = array.World.AssetsSlot;
            if (proxiesSlot.FindChild(proxySlotName) is Slot proxySlot)
            {
                ISyncList? list = null;
                foreach (var comp in proxySlot.Components)
                {
                    if (comp.GetType().IsGenericType && comp.GetType().GetGenericTypeDefinition() == typeof(ValueMultiplexer<>))
                    {
                        list = comp.GetSyncMember("Values") as ISyncList;
                        _skipListChanges = true;
                        list.World.RunSynchronously(() => _skipListChanges = false);
                        list.EnsureExactElementCount(array.Count);
                        for (int i = 0; i < array.Count; i++)
                        {
                            ((IField)list.GetElement(i)).BoxedValue = array.GetElement(i);
                        }
                    }
                    else if (comp.GetType().IsGenericType && comp.GetType().GetGenericTypeDefinition() == typeof(ReferenceMultiplexer<>))
                    {
                        list = comp.GetSyncMember("References") as ISyncList;
                        _skipListChanges = true;
                        list.World.RunSynchronously(() => _skipListChanges = false);
                        list.EnsureExactElementCount(array.Count);
                        for (int i = 0; i < array.Count; i++)
                        {
                            ((ISyncRef)list.GetElement(i)).Target = (IWorldElement)array.GetElement(i);
                        }
                    }
                    else if (comp.GetType().IsGenericType && comp.GetType().GetGenericTypeDefinition() == typeof(ValueGradientDriver<>))
                    {
                        list = comp.GetSyncMember("Points") as ISyncList;
                        _skipListChanges = true;
                        list.World.RunSynchronously(() => _skipListChanges = false);
                        list.EnsureExactElementCount(array.Count);

                        var isSyncLinear = TryGetGenericParameters(typeof(SyncLinear<>), array.GetType(), out var syncLinearGenericParameters);
                        var isSyncCurve = TryGetGenericParameters(typeof(SyncCurve<>), array.GetType(), out var syncCurveGenericParameters);
                        var syncLinearType = syncLinearGenericParameters?.First();
                        var syncCurveType = syncCurveGenericParameters?.First();
                        var isParticleBurst = syncLinearType == _particleBurstType;

                        if (!TryGetGenericParameters(typeof(SyncArray<>), array.GetType(), out var genericParameters))
                            return;

                        var arrayType = genericParameters!.Value.First();

                        for (int i = 0; i < array.Count; i++)
                        {
                            var elem = list.GetElement(i);

                            if (isSyncLinear && SupportsLerp(syncLinearType!))
                            {
                                if (isParticleBurst)
                                    SetParticlePoint((ValueGradientDriver<int2>.Point)elem!, (LinearKey<ParticleBurst>)array.GetElement(i));
                                else
                                    _setLinearPoint.MakeGenericMethod(syncLinearType).Invoke(null, [elem, array.GetElement(i)]);
                            }
                            else if (isSyncCurve && SupportsLerp(syncCurveType!))
                            {
                                _setCurvePoint.MakeGenericMethod(syncCurveType).Invoke(null, [elem, array.GetElement(i)]);
                            }
                            else
                            {
                                if (arrayType == typeof(TubePoint))
                                {
                                    SetTubePoint((ValueGradientDriver<float3>.Point)elem!, (TubePoint)array.GetElement(i));
                                }
                            }
                        }
                    }
                }
            }
        }

        private static Component GetOrAttachComponent(Slot targetSlot, Type type, out bool attachedNew)
        {
            attachedNew = false;

            if (targetSlot.GetComponent(type) is not Component comp)
            {
                comp = targetSlot.AttachComponent(type);
                attachedNew = true;
            }

            return comp;
        }

        private static bool SupportsLerp(Type type)
        {
            var coderType = typeof(Coder<>).MakeGenericType(type);
            return Traverse.Create(coderType).Property<bool>(nameof(Coder<float>.SupportsLerp)).Value;
        }

        private static bool TryGetGenericParameters(Type baseType, Type concreteType, [NotNullWhen(true)] out Sequence<Type>? genericParameters)
        {
            genericParameters = null;

            if (concreteType is null || baseType is null || !baseType.IsGenericType)
                return false;

            if (concreteType.IsGenericType && concreteType.GetGenericTypeDefinition() == baseType)
            {
                genericParameters = concreteType.GetGenericArguments();
                return true;
            }

            return TryGetGenericParameters(baseType, concreteType.BaseType, out genericParameters);
        }
    }
}