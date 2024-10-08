﻿using Elements.Core;
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

                array.Insert(buffer, startIndex);
                AddUpdateProxies(array, list, addedElements);
            };

            list.ElementsRemoved += (list, startIndex, count) => array.Remove(startIndex, count);
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

                array.Insert(buffer, startIndex);
                AddUpdateProxies(array, list, addedElements);
            };

            list.ElementsRemoved += (list, startIndex, count) => array.Remove(startIndex, count);
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

                array.Insert(buffer, startIndex);
                AddUpdateProxies(array, list, addedElements);
            };

            list.ElementsRemoved += (list, startIndex, count) => array.Remove(startIndex, count);
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

                array.Insert(buffer, startIndex);
                AddUpdateProxies(array, list, addedElements);
            };

            list.ElementsRemoved += (list, startIndex, count) => array.Remove(startIndex, count);
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

                array.Insert(buffer, startIndex);
                AddUpdateProxies(array, list, addedElements);
            };

            list.ElementsRemoved += (list, startIndex, count) => array.Remove(startIndex, count);
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

                array.Insert(buffer, startIndex);
                AddUpdateProxies(array, list, addedElements);
            };

            list.ElementsRemoved += (list, startIndex, count) => array.Remove(startIndex, count);
        }

        private static void AddUpdateProxies<T>(SyncArray<LinearKey<T>> array,
            SyncElementList<ValueGradientDriver<T>.Point> list, IEnumerable<ValueGradientDriver<T>.Point> elements)
                    where T : IEquatable<T>
        {
            foreach (var point in elements)
            {
                point.Changed += syncObject =>
                {
                    var index = list.IndexOfElement(point);
                    array[index] = new LinearKey<T>(point.Position, point.Value);
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
                    var index = list.IndexOfElement(point);
                    var key = new LinearKey<ParticleBurst>(point.Position, new ParticleBurst() { minCount = point.Value.Value.x, maxCount = point.Value.Value.y });
                    array[index] = key;
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
                    var index = list.IndexOfElement(sync);
                    array[index] = sync.Value;
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
                    var index = list.IndexOfElement(sync);
                    array[index] = sync.Target;
                };
            }
        }

        private static void AddUpdateProxies(SyncArray<TubePoint> array, SyncElementList<ValueGradientDriver<float3>.Point> list, IEnumerable<ValueGradientDriver<float3>.Point> elements)
        {
            foreach (var point in elements)
            {
                point.Changed += field =>
                {
                    var index = list.IndexOfElement(point);
                    var tubePoint = new TubePoint(point.Value.Value, point.Position.Value);
                    array[index] = tubePoint;
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
                    var index = list.IndexOfElement(point);
                    array[index] = new CurveKey<T>(point.Position, point.Value, array[index].leftTangent, array[index].rightTangent);
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
            if (proxiesSlot.FindChild(proxySlotName) is not Slot proxySlot)
            {
                proxySlot = proxiesSlot.AddSlot(proxySlotName);
                array.FindNearestParent<IDestroyable>().Destroyed += (IDestroyable _) => proxySlot.Destroy();
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

            ui.Panel().Slot.GetComponent<LayoutElement>();
            var memberFieldSlot = SyncMemberEditorBuilder.GenerateMemberField(array, name, ui, labelSize);
            ui.NestOut();
            if (!array.IsDriven)
            {
                SyncMemberEditorBuilder.BuildList(list, name, listField, ui);
                var listSlot = ui.Current;
                listSlot.DestroyWhenLocalUserLeaves();
                void ArrayChanged(IChangeable changeable)
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
                        array.Changed -= ArrayChanged;
                    }
                }
                array.Changed += ArrayChanged;
            }
            else
            {
                LocaleString text = "(array is driven)";
                ui.Text(in text);
            }

            return true;
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