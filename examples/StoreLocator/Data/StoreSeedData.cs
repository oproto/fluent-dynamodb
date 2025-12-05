using Oproto.FluentDynamoDb.Geospatial;

namespace StoreLocator.Data;

/// <summary>
/// Provides seed data for store locations in the San Francisco Bay Area.
/// Contains 50+ predefined store locations for demonstrating geospatial queries.
/// </summary>
public static class StoreSeedData
{
    /// <summary>
    /// San Francisco Bay Area bounding box for reference.
    /// Southwest: (37.2, -122.6), Northeast: (37.9, -121.8)
    /// </summary>
    public static readonly GeoBoundingBox BayArea = new(
        southwest: new GeoLocation(37.2, -122.6),
        northeast: new GeoLocation(37.9, -121.8)
    );

    /// <summary>
    /// Gets the predefined store locations in the San Francisco Bay Area.
    /// </summary>
    /// <returns>An enumerable of store data tuples (StoreId, Name, Address, Location).</returns>
    public static IEnumerable<(string StoreId, string Name, string Address, GeoLocation Location)> GetStores()
    {
        // San Francisco stores
        yield return ("SF001", "Union Square Market", "333 Post St, San Francisco, CA", new GeoLocation(37.7879, -122.4074));
        yield return ("SF002", "Fisherman's Wharf Store", "2801 Leavenworth St, San Francisco, CA", new GeoLocation(37.8080, -122.4177));
        yield return ("SF003", "Mission District Shop", "2582 Mission St, San Francisco, CA", new GeoLocation(37.7562, -122.4185));
        yield return ("SF004", "Castro Corner Store", "501 Castro St, San Francisco, CA", new GeoLocation(37.7609, -122.4350));
        yield return ("SF005", "Haight Street Market", "1556 Haight St, San Francisco, CA", new GeoLocation(37.7696, -122.4494));
        yield return ("SF006", "North Beach Deli", "1512 Stockton St, San Francisco, CA", new GeoLocation(37.8002, -122.4089));
        yield return ("SF007", "SOMA Express", "680 Folsom St, San Francisco, CA", new GeoLocation(37.7847, -122.3978));
        yield return ("SF008", "Marina Grocery", "2234 Chestnut St, San Francisco, CA", new GeoLocation(37.8005, -122.4378));
        yield return ("SF009", "Sunset Supermarket", "2425 Irving St, San Francisco, CA", new GeoLocation(37.7636, -122.4833));
        yield return ("SF010", "Richmond Market", "5625 Geary Blvd, San Francisco, CA", new GeoLocation(37.7805, -122.4785));

        // Oakland stores
        yield return ("OAK001", "Lake Merritt Market", "1960 Grand Ave, Oakland, CA", new GeoLocation(37.8116, -122.2505));
        yield return ("OAK002", "Temescal Grocery", "4900 Telegraph Ave, Oakland, CA", new GeoLocation(37.8365, -122.2615));
        yield return ("OAK003", "Jack London Square Shop", "55 Harrison St, Oakland, CA", new GeoLocation(37.7954, -122.2766));
        yield return ("OAK004", "Rockridge Market", "5655 College Ave, Oakland, CA", new GeoLocation(37.8430, -122.2518));
        yield return ("OAK005", "Fruitvale Store", "3301 E 12th St, Oakland, CA", new GeoLocation(37.7755, -122.2245));
        yield return ("OAK006", "Piedmont Avenue Shop", "4100 Piedmont Ave, Oakland, CA", new GeoLocation(37.8265, -122.2535));
        yield return ("OAK007", "Montclair Village Market", "2083 Mountain Blvd, Oakland, CA", new GeoLocation(37.8305, -122.2115));

        // Berkeley stores
        yield return ("BRK001", "Downtown Berkeley Store", "2180 Shattuck Ave, Berkeley, CA", new GeoLocation(37.8716, -122.2687));
        yield return ("BRK002", "Telegraph Ave Market", "2556 Telegraph Ave, Berkeley, CA", new GeoLocation(37.8640, -122.2585));
        yield return ("BRK003", "Solano Avenue Shop", "1799 Solano Ave, Berkeley, CA", new GeoLocation(37.8915, -122.2785));
        yield return ("BRK004", "Fourth Street Market", "1807 Fourth St, Berkeley, CA", new GeoLocation(37.8695, -122.3015));
        yield return ("BRK005", "Elmwood District Store", "2930 College Ave, Berkeley, CA", new GeoLocation(37.8585, -122.2515));

        // San Jose stores
        yield return ("SJ001", "Downtown San Jose Market", "150 S First St, San Jose, CA", new GeoLocation(37.3352, -121.8911));
        yield return ("SJ002", "Santana Row Shop", "377 Santana Row, San Jose, CA", new GeoLocation(37.3215, -121.9475));
        yield return ("SJ003", "Willow Glen Store", "1165 Lincoln Ave, San Jose, CA", new GeoLocation(37.3085, -121.9015));
        yield return ("SJ004", "Japantown Market", "565 N 6th St, San Jose, CA", new GeoLocation(37.3485, -121.8855));
        yield return ("SJ005", "Almaden Valley Shop", "6955 Almaden Expy, San Jose, CA", new GeoLocation(37.2385, -121.8615));
        yield return ("SJ006", "Evergreen Plaza Store", "2747 Aborn Rd, San Jose, CA", new GeoLocation(37.3115, -121.7855));

        // Palo Alto / Mountain View stores
        yield return ("PA001", "University Avenue Market", "261 University Ave, Palo Alto, CA", new GeoLocation(37.4445, -122.1615));
        yield return ("PA002", "California Avenue Shop", "367 California Ave, Palo Alto, CA", new GeoLocation(37.4285, -122.1425));
        yield return ("PA003", "Stanford Shopping Center", "180 El Camino Real, Palo Alto, CA", new GeoLocation(37.4435, -122.1715));
        yield return ("MV001", "Castro Street Market", "193 Castro St, Mountain View, CA", new GeoLocation(37.3935, -122.0795));
        yield return ("MV002", "San Antonio Center Shop", "2550 W El Camino Real, Mountain View, CA", new GeoLocation(37.4015, -122.1115));

        // Fremont / Union City stores
        yield return ("FRE001", "Fremont Hub Store", "39281 Fremont Hub, Fremont, CA", new GeoLocation(37.5485, -121.9885));
        yield return ("FRE002", "Niles District Market", "37592 Niles Blvd, Fremont, CA", new GeoLocation(37.5765, -121.9765));
        yield return ("FRE003", "Warm Springs Shop", "46601 Mission Blvd, Fremont, CA", new GeoLocation(37.4915, -121.9385));
        yield return ("UC001", "Union City Market", "32100 Union Landing Blvd, Union City, CA", new GeoLocation(37.5935, -122.0185));

        // Walnut Creek / Concord stores
        yield return ("WC001", "Broadway Plaza Store", "1275 Broadway Plaza, Walnut Creek, CA", new GeoLocation(37.9015, -122.0615));
        yield return ("WC002", "Downtown Walnut Creek Shop", "1501 N California Blvd, Walnut Creek, CA", new GeoLocation(37.9065, -122.0655));
        yield return ("CON001", "Todos Santos Plaza Market", "2151 Salvio St, Concord, CA", new GeoLocation(37.9785, -122.0315));
        yield return ("CON002", "Sunvalley Mall Store", "1 Sunvalley Mall, Concord, CA", new GeoLocation(37.9615, -122.0115));

        // San Mateo / Redwood City stores
        yield return ("SM001", "Downtown San Mateo Market", "200 S B St, San Mateo, CA", new GeoLocation(37.5635, -122.3235));
        yield return ("SM002", "Hillsdale Shopping Center", "60 31st Ave, San Mateo, CA", new GeoLocation(37.5385, -122.2985));
        yield return ("RWC001", "Downtown Redwood City Shop", "2200 Broadway, Redwood City, CA", new GeoLocation(37.4865, -122.2285));
        yield return ("RWC002", "Sequoia Station Store", "1015 El Camino Real, Redwood City, CA", new GeoLocation(37.4785, -122.2315));

        // Daly City / South San Francisco stores
        yield return ("DC001", "Serramonte Center Market", "3 Serramonte Center, Daly City, CA", new GeoLocation(37.6715, -122.4715));
        yield return ("SSF001", "South San Francisco Store", "180 El Camino Real, South San Francisco, CA", new GeoLocation(37.6535, -122.4085));

        // Hayward / San Leandro stores
        yield return ("HAY001", "Downtown Hayward Market", "22300 Foothill Blvd, Hayward, CA", new GeoLocation(37.6685, -122.0815));
        yield return ("HAY002", "Southland Mall Store", "1 Southland Mall, Hayward, CA", new GeoLocation(37.6315, -122.0515));
        yield return ("SL001", "San Leandro Store", "1919 Davis St, San Leandro, CA", new GeoLocation(37.7255, -122.1565));
        yield return ("SL002", "Bayfair Center Shop", "15555 E 14th St, San Leandro, CA", new GeoLocation(37.6965, -122.1215));
    }

    /// <summary>
    /// Gets the total count of seed stores.
    /// </summary>
    public static int StoreCount => 51;
}
