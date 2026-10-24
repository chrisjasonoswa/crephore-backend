namespace Crephore.Api.Common.Api;


/*
 * This class serves as a centralized location for defining API route constants.
 * It helps maintain consistency across the application and makes it easier to manage routes.
 * Each resource and sub-resource has its own nested static class for better organization.
 *
 * Properties:
 *      1. Prefix:
 *              The common prefix for all API routes - "api".
 *              This is already handled by RoutePrefixConvention
 *              so it should not be included in the route definitions for controllers.
 *      2. Route:
 *              The main specific name for the route (e.g., "auth", "events").
 *              Unlike RelativeRoute, this will contain its parent resource in the case of sub-routes (e.g., "events/guests").
 *      3. RelativeRoute:
 *              The relative path for sub-routes under a main route (e.g., "signup", "login", "logout" under "auth").
 *      4. FullPath: The complete path for the route, combining the prefix and resource (e.g., "api/auth/login").
 *
 */
public static class ApiRoutes
{
    public const string Prefix = "api";

    public static class Auth
    {
        public const string Route = "auth";
        public const string FullPath = $"{Prefix}/{Route}";

        //Sub resources
        public static class Signup
        {
            public const string RelativeRoute = "signup";
            public const string FullPath = $"{Auth.FullPath}/{RelativeRoute}";
        }

        public static class Login
        {
            public const string RelativeRoute = "login";
            public const string FullPath = $"{Auth.FullPath}/{RelativeRoute}";
        }

        public static class Logout
        {
            public const string RelativeRoute = "logout";
            public const string FullPath = $"{Auth.FullPath}/{RelativeRoute}";
        }

        public static class Me
        {
            public const string RelativeRoute = "me";
            public const string FullPath = $"{Auth.FullPath}/{RelativeRoute}";
        }
    }
}
