kubectl apply -f deployment-main.yaml -n biatec-tokens
kubectl delete configmap biatec-tokens-api-main-conf -n biatec-tokens
kubectl create configmap biatec-tokens-api-main-conf --from-file=conf -n biatec-tokens
kubectl rollout restart deployment/biatec-tokens-api-main-app-deployment -n biatec-tokens
kubectl rollout status deployment/biatec-tokens-api-main-app-deployment -n biatec-tokens
