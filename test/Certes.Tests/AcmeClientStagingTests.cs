﻿using System.Linq;
using System.Threading.Tasks;
using Certes.Acme;
using Certes.Jws;
using Certes.Pkcs;
using Xunit;

namespace Certes
{
    public class AcmeClientStagingTests
    {
        [Theory]
        [InlineData(SignatureAlgorithm.RS256)]
        [InlineData(SignatureAlgorithm.ES256)]
        [InlineData(SignatureAlgorithm.ES384)]
        public async Task RunAccountFlow(SignatureAlgorithm algorithm)
        {
            var key = new AccountKey(algorithm);
            using (var client = new AcmeClient(await Helper.GetStagingServer()))
            {
                client.Use(key.Export());
                var reg = await client.NewRegistraton();

                var newKey = new AccountKey().Export();
                await client.ChangeKey(reg, newKey);
                await client.DeleteRegistration(reg);
            }
        }

        [Fact]
        public async Task CanIssueSan()
        {
            var accountKey = await Helper.Loadkey();
            var csr = new CertificationRequestBuilder();
            csr.AddName("CN=CA, ST=Ontario, L=Toronto, O=Certes, OU=Dev, CN=www.certes-ci.dymetis.com");
            csr.SubjectAlternativeNames.Add("mail.certes-ci.dymetis.com");
            csr.SubjectAlternativeNames.Add("sso.certes-ci.dymetis.com");

            using (var client = new AcmeClient(await Helper.GetStagingServer()))
            {
                client.Use(accountKey.Export());

                await Task.WhenAll(
                    AuthorizeDns(client, "www.certes-ci.dymetis.com"),
                    AuthorizeDns(client, "mail.certes-ci.dymetis.com"),
                    AuthorizeDns(client, "sso.certes-ci.dymetis.com"));

                var cert = await client.NewCertificate(csr);
            }
        }

        private static async Task AuthorizeDns(AcmeClient client, string name)
        {
            var authz = await client.NewAuthorization(new AuthorizationIdentifier
            {
                Type = AuthorizationIdentifierTypes.Dns,
                Value = name
            });

            var httpChallengeInfo = authz.Data.Challenges
                .Where(c => c.Type == ChallengeTypes.Http01).First();
            var httpChallenge = await client.CompleteChallenge(httpChallengeInfo);

            while (authz.Data.Status == EntityStatus.Pending)
            {
                // Wait for ACME server to validate the identifier
                await Task.Delay(1000);
                authz = await client.GetAuthorization(httpChallenge.Location);
            }

            Assert.Equal(EntityStatus.Valid, authz.Data.Status);
        }
    }
}
