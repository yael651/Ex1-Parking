FOR /F "tokens=* USEBACKQ" %%F IN (`aws configure get region`) DO (
SET regionName=%%F
)

aws s3api create-bucket --bucket yaelbucketsaws --region %regionName% --create-bucket-configuration LocationConstraint=%regionName%
dotnet build
dotnet lambda deploy-serverless ParkingLotYaelLivni --region %regionName% --s3-bucket yaelbucketsaws
