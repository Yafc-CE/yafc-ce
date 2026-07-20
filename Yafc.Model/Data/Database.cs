using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Yafc.Model;

public static class Database {
    // null-forgiveness for all not-null properties; they're initialized by LoadBuiltData.
    // ----------------------------------------------------------------
    // For caching, the properties in this section must match (by name and cache storage type) the corresponding parameter to LoadBuiltData.
    public static int constantCombinatorCapacity { get; private set; } = 18; // Keep this initializer for the tests.
    public static FactorioObject[] rootAccessible { get; private set; } = null!;
    public static IObjectWithQuality<Special> heat { get; private set; } = null!; // Only the Special is stored in the cache.
    public static Dictionary<string, List<Fluid>> fluidVariants { get; private set; } = null!;
    // End of the cache-dependent section. The remaining static properties are calculated based on the method parameters and known names.
    // ----------------------------------------------------------------
    public static Dictionary<string, FactorioObject> objectsByTypeName { get; private set; } = null!;
    public static Item[] allSciencePacks { get; private set; } = null!;
    public static List<Special>? heatVariants { get; private set; }
    public static IObjectWithQuality<Goods> voidEnergy { get; private set; } = null!;
    public static IObjectWithQuality<Item> science { get; private set; } = null!;
    public static IObjectWithQuality<Item> itemInput { get; private set; } = null!;
    public static IObjectWithQuality<Item> itemOutput { get; private set; } = null!;
    public static IObjectWithQuality<Special> electricity { get; private set; } = null!;
    public static IObjectWithQuality<Recipe> electricityGeneration { get; private set; } = null!;
    public static Entity? character { get; private set; }
    public static Special planetSurface { get; private set; } = null!;
    public static EntityCrafter[] allCrafters { get; private set; } = null!;
    public static Module[] allModules { get; private set; } = null!;
    public static EntityBeacon[] allBeacons { get; private set; } = null!;
    public static EntityBelt[] allBelts { get; private set; } = null!;
    public static EntityInserter[] allInserters { get; private set; } = null!;
    public static EntityAccumulator[] allAccumulators { get; private set; } = null!;
    public static EntityContainer[] allContainers { get; private set; } = null!;
    public static FactorioIdRange<FactorioObject> objects { get; private set; } = null!;
    public static FactorioIdRange<Goods> goods { get; private set; } = null!;
    public static FactorioIdRange<Special> specials { get; private set; } = null!;
    public static FactorioIdRange<Item> items { get; private set; } = null!;
    public static FactorioIdRange<Fluid> fluids { get; private set; } = null!;
    public static FactorioIdRange<Recipe> recipes { get; private set; } = null!;
    public static FactorioIdRange<Mechanics> mechanics { get; private set; } = null!;
    public static FactorioIdRange<RecipeOrTechnology> recipesAndTechnologies { get; private set; } = null!;
    public static FactorioIdRange<Technology> technologies { get; private set; } = null!;
    public static FactorioIdRange<Entity> entities { get; private set; } = null!;
    public static FactorioIdRange<Quality> qualities { get; private set; } = null!;
    public static FactorioIdRange<Location> locations { get; private set; } = null!;

    /// <summary>
    /// Returns the set of beacons filtered to only those that can accept at least one module.
    /// </summary>
    public static IEnumerable<EntityBeacon> usableBeacons => allBeacons.Where(b => allModules.Any(m => b.CanAcceptModule(m.moduleSpecification)));

    /// <summary>
    /// Fetches a module that can be used in this beacon, or <see langword="null"/> if no beacon was specified or no module could be found.
    /// </summary>
    /// <param name="beacon">The beacon to receive a module. If <see langword="null"/>, <paramref name="module"/> will be set to null and this
    /// method will return <see langword="false"/>.</param>
    /// <param name="module">A module that can be placed in that beacon, if such a module exists.</param>
    /// <returns><see langword="true"/> if a module could be found, or <see langword="false"/> if the supplied beacon does not accept any modules or was <see langword="null"/>.</returns>
    public static bool GetDefaultModuleFor(EntityBeacon? beacon, [NotNullWhen(true)] out Module? module) {
        module = allModules.FirstOrDefault(m => EntityWithModules.CanAcceptModule(m.moduleSpecification, beacon?.allowedEffects ?? AllowedEffects.None, null));
        return module != null;
    }

    public static FactorioObject? FindClosestVariant(string id) {
        string baseId;
        int temperature;
        int splitter = id.IndexOf('@');
        if (splitter >= 0) {
            baseId = id[..splitter];
            _ = int.TryParse(id[(splitter + 1)..], out temperature);
        }
        else {
            baseId = id;
            temperature = 0;
        }

        if (objectsByTypeName.TryGetValue(baseId, out var result)) {
            return result;
        }

        if (fluidVariants.TryGetValue(baseId, out var variants)) {
            var prev = variants[0];
            for (int i = 1; i < variants.Count; i++) {
                var cur = variants[i];
                if (cur.temperature >= temperature) {
                    return cur.temperature - temperature > temperature - prev.temperature ? prev : cur;
                }

                prev = cur;
            }
            return prev;
        }

        if (heatVariants != null && heatVariants[0].typeDotName.StartsWith(baseId + "@", StringComparison.Ordinal)) {
            var prev = heatVariants[0];
            for (int i = 1; i < heatVariants.Count; i++) {
                var cur = heatVariants[i];
                if (cur.temperature >= temperature) {
                    return cur.temperature - temperature > temperature - prev.temperature ? prev : cur;
                }

                prev = cur;
            }
            return prev;
        }

        return null;
    }

    /// <summary>
    /// Initializes the <see cref="Database"/> class from the supplied parameters. For caching to work, most parameters must be stored in a readable
    /// property with the same name and cache storage type.
    /// </summary>
    /// <param name="constantCombinatorCapacity">The number of values that can be set in a single constant combinator.</param>
    /// <param name="rootAccessible">The objects that are inherently accessible (character, nauvis, void, etc.).</param>
    /// <param name="allObjects">The list of all objects. This does not have a matching property, and must accept a List&lt;FactorioObject>. Renaming
    /// this parameter requires matching changes in the cache reader and writer.</param>
    /// <param name="formerAliases">The old names of objects that were renamed by Yafc in the process of development.
    /// This does not have a matching property, and must have the same cache storage type as a Dictionary&lt;string, FactorioObject>. Renaming this
    /// parameter requires matching changes in the cache writer.</param>
    /// <param name="heat">The original heat object, which has probably been renamed heat@&lt;temperature>.</param>
    /// <param name="fluidVariants">The collection of all fluid variants from their original name.</param>
    // This method's parameters must be cachable objects. Notably, IObjectWithQuality is not currently cachable.
    // Changing this method signature will invalidate existing caches. Except as noted above, the reader and writer will automatically adapt.
    internal static void LoadBuiltData(int constantCombinatorCapacity, FactorioObject[] rootAccessible, List<FactorioObject> allObjects,
        Dictionary<string, FactorioObject> formerAliases, Special heat, Dictionary<string, List<Fluid>> fluidVariants) {

        Database.constantCombinatorCapacity = constantCombinatorCapacity;
        Database.rootAccessible = rootAccessible;
        objectsByTypeName = allObjects.ToDictionary(x => x.typeDotName);
        foreach (var alias in formerAliases) {
            _ = objectsByTypeName.TryAdd(alias.Key, alias.Value);
        }

        // null-forgiving: inputs is always initialized for labs
        allSciencePacks = [.. allObjects.OfType<EntityCrafter>().Where(c => c.factorioType == "lab").SelectMany(l => l.inputs!).Distinct()];
        voidEnergy = ((Goods)objectsByTypeName["Power." + SpecialNames.Void]).With(Quality.Normal);
        science = ((Item)objectsByTypeName["Item.science"]).With(Quality.Normal);
        itemInput = ((Item)objectsByTypeName["Item.item-total-input"]).With(Quality.Normal);
        itemOutput = ((Item)objectsByTypeName["Item.item-total-output"]).With(Quality.Normal);
        electricity = ((Special)objectsByTypeName["Power." + SpecialNames.Electricity]).With(Quality.Normal);
        electricityGeneration = ((Recipe)objectsByTypeName[$"Mechanics.{SpecialNames.GeneratorRecipe}.{SpecialNames.Electricity}"]).With(Quality.Normal);
        Database.heat = heat.With(Quality.Normal);
        if (objectsByTypeName.TryGetValue("Entity.character", out var ch)) {
            character = (Entity)ch;
        }
        planetSurface = (Special)objectsByTypeName[$"Special.{SpecialNames.PlanetSurface}"];

        int firstSpecial = 0;
        int firstItem = skip(firstSpecial, FactorioObjectSortOrder.SpecialGoods);
        int firstFluid = skip(firstItem, FactorioObjectSortOrder.Items);
        int firstRecipe = skip(firstFluid, FactorioObjectSortOrder.Fluids);
        int firstMechanics = skip(firstRecipe, FactorioObjectSortOrder.Recipes);
        int firstTechnology = skip(firstMechanics, FactorioObjectSortOrder.Mechanics);
        int firstEntity = skip(firstTechnology, FactorioObjectSortOrder.Technologies);
        int firstTile = skip(firstEntity, FactorioObjectSortOrder.Entities);
        int firstQuality = skip(firstTile, FactorioObjectSortOrder.Tiles);
        int firstLocation = skip(firstQuality, FactorioObjectSortOrder.Qualities);
        int firstTrigger = skip(firstLocation, FactorioObjectSortOrder.Locations);
        int last = skip(firstTrigger, FactorioObjectSortOrder.Triggers);
        if (last != allObjects.Count) {
            throw new Exception("Something is not right");
        }

        objects = new FactorioIdRange<FactorioObject>(0, last, allObjects);
        specials = new FactorioIdRange<Special>(firstSpecial, firstItem, allObjects);
        items = new FactorioIdRange<Item>(firstItem, firstFluid, allObjects);
        fluids = new FactorioIdRange<Fluid>(firstFluid, firstRecipe, allObjects);
        goods = new FactorioIdRange<Goods>(firstSpecial, firstRecipe, allObjects);
        recipes = new FactorioIdRange<Recipe>(firstRecipe, firstTechnology, allObjects);
        mechanics = new FactorioIdRange<Mechanics>(firstMechanics, firstTechnology, allObjects);
        recipesAndTechnologies = new FactorioIdRange<RecipeOrTechnology>(firstRecipe, firstEntity, allObjects);
        technologies = new FactorioIdRange<Technology>(firstTechnology, firstEntity, allObjects);
        entities = new FactorioIdRange<Entity>(firstEntity, firstTile, allObjects);
        qualities = new FactorioIdRange<Quality>(firstQuality, firstLocation, allObjects);
        locations = new FactorioIdRange<Location>(firstLocation, firstTrigger, allObjects);
        Database.fluidVariants = fluidVariants;
        heatVariants = heat.variants;

        allModules = [.. items.all.OfType<Module>()];
        allBeacons = [.. entities.all.OfType<EntityBeacon>()];
        allCrafters = [.. entities.all.OfType<EntityCrafter>()];
        allBelts = [.. entities.all.OfType<EntityBelt>()];
        allInserters = [.. entities.all.OfType<EntityInserter>()];
        allAccumulators = [.. entities.all.OfType<EntityAccumulator>()];
        allContainers = [.. entities.all.OfType<EntityContainer>()];

        int skip(int from, FactorioObjectSortOrder sortOrder) {
            for (; from < allObjects.Count; from++) {
                if (allObjects[from].sortingOrder != sortOrder) {
                    break;
                }
            }

            return from;
        }
    }
}

// Since Factorio objects are sorted by type, objects of one type always occupy continuous range.
// Because of that we can replace Dictionary<SomeFactorioObjectSubtype, T> with just plain array indexed with object id with range offset
// This is mostly useful for analysis algorithms that needs a bunch of these constructs
public class FactorioIdRange<T> where T : FactorioObject {
    internal readonly int start;
    public int count { get; }
    public T[] all { get; }
    public IEnumerable<T> explorable => all.Where(o => o.showInExplorers);

    public FactorioIdRange(int start, int end, List<FactorioObject> source) {
        this.start = start;
        count = end - start;
        all = new T[count];
        for (int i = 0; i < count; i++) {
            all[i] = (T)source[i + start];
        }
    }

    public T this[int i] => all[i];
    public T this[FactorioId id] => all[(int)id - start];

    public Mapping<T, TValue> CreateMapping<TValue>() => new Mapping<T, TValue>(this);

    public Mapping<T, TOther, TValue> CreateMapping<TOther, TValue>(FactorioIdRange<TOther> other) where TOther : FactorioObject => new Mapping<T, TOther, TValue>(this, other);

    public Mapping<T, TValue> CreateMapping<TValue>(Func<T, TValue> mapFunc) {
        var map = CreateMapping<TValue>();
        foreach (var o in all) {
            map[o] = mapFunc(o);
        }

        return map;
    }
}

// Mapping[TKey, TValue] is almost like a dictionary where TKey is FactorioObject but it is an array wrapper and therefore very fast.
// This is preferable way to add custom properties to FactorioObjects
public readonly struct Mapping<TKey, TValue>(FactorioIdRange<TKey> source) : IDictionary<TKey, TValue> where TKey : FactorioObject {
    private readonly int offset = source.start;
    private readonly FactorioIdRange<TKey> source = source;

    public void Add(TKey key, TValue value) => this[key] = value;

    public bool ContainsKey(TKey key) => true;

    public bool Remove(TKey key) => throw new NotSupportedException();

    public bool TryGetValue(TKey key, out TValue value) {
        value = this[key];
        return true;
    }

    TValue IDictionary<TKey, TValue>.this[TKey key] {
        get => this[key];
        set => this[key] = value;
    }

    public Mapping<TKey, TOther> Remap<TOther>(Func<TKey, TValue, TOther> remap) {
        var remapped = source.CreateMapping<TOther>();
        foreach (var key in source.all) {
            remapped[key] = remap(key, this[key]);
        }

        return remapped;
    }

    public ref TValue this[TKey index] => ref Values[(int)index.id - offset];
    public ref TValue this[FactorioId id] => ref Values[(int)(id - offset)];
    //public ref TValue this[int id] => ref data[id];
    public void Clear() => Array.Clear(Values, 0, Values.Length);

    public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);

    public int Count => Values.Length;
    public bool IsReadOnly => false;
    public TKey[] Keys => source.all;
    public TValue[] Values { get; } = new TValue[source.count];
    ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys;
    ICollection<TValue> IDictionary<TKey, TValue>.Values => Values;
    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => GetEnumerator();

    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add(KeyValuePair<TKey, TValue> item) => this[item.Key] = item.Value;

    public bool Contains(KeyValuePair<TKey, TValue> item) => EqualityComparer<TValue>.Default.Equals(this[item.Key], item.Value);

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
        for (int i = 0; i < Values.Length; i++) {
            array[i + arrayIndex] = new KeyValuePair<TKey, TValue>(source[i], Values[i]);
        }
    }

    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>> {
        private int index;
        private readonly TKey[] keys;
        private readonly TValue[] values;
        internal Enumerator(Mapping<TKey, TValue> mapping) {
            index = -1;
            keys = mapping.source.all;
            values = mapping.Values;
        }
        public bool MoveNext() => ++index < keys.Length;

        public void Reset() => index = -1;

        public readonly KeyValuePair<TKey, TValue> Current => new KeyValuePair<TKey, TValue>(keys[index], values[index]);
        readonly object IEnumerator.Current => Current;
        public readonly void Dispose() { }
    }
}

public readonly struct Mapping<TKey1, TKey2, TValue> where TKey1 : FactorioObject where TKey2 : FactorioObject {
    private readonly int offset1, offset2, count1;
    private readonly TValue[] data;
    internal Mapping(FactorioIdRange<TKey1> key1, FactorioIdRange<TKey2> key2) {
        offset1 = key1.start;
        offset2 = key2.start;
        count1 = key1.count;
        data = new TValue[count1 * key2.count];
    }
    public ref TValue this[TKey1 x, TKey2 y] => ref data[(((int)x.id - offset1) * count1) + ((int)y.id - offset2)];

    public void CopyRow(TKey1 from, TKey1 to) {
        if (from == to) {
            return;
        }

        int fromId = ((int)from.id - offset1) * count1;
        int toId = ((int)to.id - offset1) * count1;
        Array.Copy(data, fromId, data, toId, count1);
    }

    public ArraySegment<TValue> GetSlice(TKey1 row) => new ArraySegment<TValue>(data, ((int)row.id - offset1) * count1, count1);

    public FactorioId IndexToId(int index) => (FactorioId)(index + offset2);
}

public static class SpecialNames {
    public const string BurnableFluid = "burnable-fluid.";
    public const string Heat = "heat";
    public const string Void = "void";
    public const string Electricity = "electricity";
    public const string HotFluid = "hot-fluid";
    public const string SpecificFluid = "fluid.";
    public const string MiningRecipe = "mining.";
    public const string BoilerRecipe = "boiler.";
    public const string FakeRecipe = "fake-recipe";
    public const string FixedRecipe = "fixed-recipe.";
    public const string GeneratorRecipe = "generator";
    public const string PumpingRecipe = "pump.";
    public const string Labs = "labs.";
    public const string TechnologyTrigger = "technology-trigger";
    public const string RocketLaunch = "launch";
    public const string RocketCraft = "rocket.";
    public const string ReactorRecipe = "reactor";
    public const string SpoilRecipe = "spoil";
    public const string PlantRecipe = "plant";
    public const string AsteroidCapture = "asteroid-capture";
    public const string PlanetSurface = "planet-surface";
}
